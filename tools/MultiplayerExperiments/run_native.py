#!/usr/bin/env python3
"""Run the registered manual multiplayer pilot through the native game launchers."""

import argparse
import json
import platform
import subprocess
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
PROTOCOL_DIR = REPO / "docs/strategy/multiplayer-experiments-20260923"


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(data, indent=2) + "\n")
    temporary.replace(path)


def prepare(out, admitted):
    protocol = json.loads((PROTOCOL_DIR / "protocol.json").read_text())
    roots = json.loads((PROTOCOL_DIR / "root-candidates.json").read_text())["candidates"]
    manifest = (PROTOCOL_DIR / "root-candidates.json").resolve()
    trials = []
    for root in roots:
        for peer in protocol["pilot"]["partnerPolicyIds"]:
            for cell in protocol["pilot"]["interactionCells"]:
                pair = "__".join((root["id"], peer, cell["scheduleId"], cell["requestCadenceId"]))
                pair_number = len(trials) // 2
                for credit in ([False, True] if pair_number % 2 == 0 else [True, False]):
                    arm = "shared_on" if credit else "shared_off"
                    label = pair + "__" + arm
                    options = dict(
                        schemaVersion=1, rootManifestPath=str(manifest), candidateId=root["id"],
                        partnerPolicy=peer, schedule=cell["scheduleId"],
                        requestCadence=cell["requestCadenceId"], profile="smoke_350", mode="Pilot",
                        creditSharedDamage=credit, enemyCycleLimit=12, decisionLimit=256, queryLimit=24,
                        expectedOpeningPath=str(Path(admitted[root["id"]]).resolve(strict=True)),
                    )
                    config = out / "inputs" / (label + ".json")
                    request = out / "inputs" / (label + "-request.json")
                    write_json(config, options)
                    write_json(request, dict(scenarioId="MULTIPLAYER-MANUAL-LOOP", cards=[],
                                             multiplayerExperimentPath=str(config), timeoutSeconds=120))
                    trials.append(dict(label=label, pair=pair, arm=arm, root=root["id"],
                                       peer=peer, **cell, config=str(config), request=str(request)))
    if len(trials) != protocol["pilot"]["plannedTrajectories"]:
        raise ValueError("Registered pilot count differs from the generated trial plan.")
    write_json(out / "plan.json", dict(protocolId=protocol["protocolId"], trials=trials,
                                      sourceCluster=roots and "controlled-ironclad-silent-template-v1",
                                      humanSources=0, plannedBeforeExecution=True))
    return trials


def run_macos(trials, out, pending):
    # Keep a separate warmup and bound native scene reuse to two measured pairs.
    for chunk_index, start in enumerate(range(0, len(pending), 4), 1):
        chunk = pending[start:start + 4]
        batch = out / f"batch-{chunk_index:02d}"
        warm_config = out / "inputs" / f"warmup-{chunk_index:02d}.json"
        warm_request = out / "inputs" / f"warmup-{chunk_index:02d}-request.json"
        options = json.loads(Path(chunk[0]["config"]).read_text())
        options.update(candidateId="defense_investment_control", mode="Contract", expectedOpeningPath=None,
                       partnerPolicy="immediate_attack", schedule="alternate_one_action",
                       requestCadence="after_peer_batch", creditSharedDamage=True)
        write_json(warm_config, options)
        write_json(warm_request, dict(scenarioId="MULTIPLAYER-MANUAL-LOOP", cards=[],
                                     multiplayerExperimentPath=str(warm_config), timeoutSeconds=120))
        command = [str(REPO / "tools/run-unattended-test-macos.sh"), str(warm_request)]
        command.extend(trial["request"] for trial in chunk)
        command += ["--cleanup-instance-on-exit", "--timeout-seconds", "120", "--output-dir", str(batch)]
        for index, trial in enumerate(chunk, 2):
            trial.update(evidence=str(batch / f"{index:02d}-evidence"),
                         result=str(batch / f"{index:02d}-result.json"))
        write_json(out / "execution.json", trials)
        with (out / f"batch-{chunk_index:02d}.log").open("w") as log:
            process = subprocess.run(command, cwd=REPO, stdout=log, stderr=subprocess.STDOUT, check=False)
        print(f"batch {chunk_index}: exit={process.returncode}", flush=True)
        collect(trials, out)
        if process.returncode:
            raise SystemExit("Batch stopped; all planned arms remain in results, including unexecuted arms.")


def run_other(trials, out, pending):
    for index, trial in enumerate(pending, 1):
        evidence = out / "runs" / trial["label"]
        trial.update(evidence=str(evidence), result=str(evidence / "result.json"))
        if platform.system() == "Windows":
            command = ["pwsh", "-NoProfile", "-File", str(REPO / "tools/run-unattended-test.ps1"),
                       "-ScenarioId", "MULTIPLAYER-MANUAL-LOOP", "-MultiplayerExperimentPath", trial["config"],
                       "-EvidenceDirectory", str(evidence), "-TimeoutSeconds", "120", "-CleanupInstanceOnExit"]
        else:
            command = [str(REPO / "tools/run-unattended-test.sh"), "--scenario-id", "MULTIPLAYER-MANUAL-LOOP",
                       "--multiplayer-experiment-path", trial["config"], "--evidence-directory", str(evidence),
                       "--timeout-seconds", "120", "--cleanup-instance-on-exit"]
        write_json(out / "execution.json", trials)
        with (out / f"trial-{index:02d}.log").open("w") as log:
            process = subprocess.run(command, cwd=REPO, stdout=log, stderr=subprocess.STDOUT, check=False)
        print(f"trial {index}: exit={process.returncode}", flush=True)
        collect(trials, out)
        if process.returncode:
            raise SystemExit("Trial stopped; inspect the recorded boundary before continuing.")


def collect(trials, out):
    rows = []
    for trial in trials:
        row = {k: v for k, v in trial.items() if k not in ("request", "config")}
        result = Path(trial["result"]) if trial.get("result") else None
        artifact = Path(trial["evidence"]) / "multiplayer-experiment.json" if trial.get("evidence") else None
        launcher_path = artifact.parent / "launcher-result.json" if artifact else None
        launcher = json.loads(launcher_path.read_text()) if launcher_path and launcher_path.is_file() else None
        stopped_outcome = "request_timeout" if launcher and launcher.get("status") == "timeout" else "execution_error"
        row.update(status="NotExecuted", outcome="not_executed")
        if result and result.is_file():
            native = json.loads(result.read_text())
            row.update(status=native["status"], error=native.get("error"),
                       elapsedMs=native.get("elapsedMilliseconds"))
        if artifact and artifact.is_file():
            data = json.loads(artifact.read_text())
            row.update(outcome=data["outcome"], nativeEnded=data["nativeEnded"], nativeWon=data["nativeWon"],
                       round=data["round"], health=data["health"], invalidAdvice=data["invalidAdvice"],
                       localActions=data["localActions"], peerActions=data["peerActions"],
                       queryCount=len(data["queries"]),
                       stageCovered=sum(q["stageCovered"] for q in data["queries"]),
                       timeLimitedQueries=sum(q["turnLayerTimeBudgetStops"] > 0 or q["boundary"] == "TimeLimit"
                                              for q in data["queries"]),
                       expandedNodes=sum(q["expandedNodes"] for q in data["queries"]),
                       transitions=sum(q["transitionCount"] for q in data["queries"]),
                       queries=[{k: v for k, v in q.items() if k not in ("root", "policy")}
                                for q in data["queries"]])
            if not result.is_file():
                row.update(status="Failed", outcome=stopped_outcome,
                           error="Native launcher ended without a result; partial observations only.",
                           launcher=launcher)
        elif result and result.is_file():
            error = row.get("error") or ""
            row["outcome"] = "request_timeout" if "Timeout" in error else "execution_error"
        elif launcher or artifact and artifact.parent.joinpath("multiplayer-opening.json").is_file():
            # A staged request alone does not prove it ran. Preserve an observed stop without guessing why.
            row.update(status="Failed", outcome=stopped_outcome, launcher=launcher,
                       error="Native result unavailable; inspect the launcher receipt and last observed boundary.")
        rows.append(row)
    write_json(out / "results.json", rows)
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--admitted-openings", type=Path)
    parser.add_argument("--collect-only", action="store_true")
    parser.add_argument("--continue-unexecuted-from", type=Path)
    args = parser.parse_args()
    out = args.output.resolve()
    if args.collect_only:
        execution = out / "execution.json"
        trials = json.loads(execution.read_text()) if execution.is_file() else json.loads((out / "plan.json").read_text())["trials"]
        collect(trials, out)
        return
    if out.exists():
        raise FileExistsError("Use a new output directory; existing experiment evidence is immutable.")
    if args.admitted_openings is None and args.continue_unexecuted_from is None:
        parser.error("--admitted-openings is required for execution")
    out.mkdir(parents=True)
    if args.continue_unexecuted_from:
        prior = args.continue_unexecuted_from.resolve()
        trials = json.loads((prior / "execution.json").read_text())
        previous_rows = collect(trials, prior)
        pending_labels = {r["label"] for r in previous_rows if r["status"] == "NotExecuted"}
        pending = [r for r in trials if r["label"] in pending_labels]
        write_json(out / "plan.json", dict(continuedFrom=str(prior), trials=trials,
                                          pendingLabels=sorted(pending_labels), preservesFailedArms=True))
    else:
        trials = prepare(out, json.loads(args.admitted_openings.read_text()))
        pending = trials
    write_json(out / "execution.json", trials)
    collect(trials, out)
    (run_macos if platform.system() == "Darwin" else run_other)(trials, out, pending)


if __name__ == "__main__":
    main()
