#!/usr/bin/env python3
"""Compare every registered pair, preserving failures and nonterminal stops."""

import argparse
import collections
import copy
import json
from pathlib import Path


def load(path):
    return json.loads(Path(path).read_text())


def action_signature(action):
    return {key: action[key] for key in (
        "kind", "turn", "cardId", "cardOccurrence", "cardStateKey", "cardStateOccurrence",
        "targetCombatId", "potionId", "potionSlot", "choices", "turnStartChoices")}


def first_difference(left, right, path=""):
    if type(left) is not type(right):
        return path
    if isinstance(left, dict):
        if left.keys() != right.keys():
            return path + "/keys"
        for key in left:
            difference = first_difference(left[key], right[key], path + "/" + key)
            if difference is not None:
                return difference
    elif isinstance(left, list):
        if len(left) != len(right):
            return path + "/length"
        for index, (a, b) in enumerate(zip(left, right)):
            difference = first_difference(a, b, path + "/" + str(index))
            if difference is not None:
                return difference
    elif left != right:
        return path
    return None


def aa(left, right):
    a, b = copy.deepcopy(load(left)), copy.deepcopy(load(right))
    for row in (a, b):
        for field in ("rootManifestPath", "expectedOpeningPath", "expectedFirstCardId"):
            row["spec"]["options"].pop(field, None)
        for query in row["queries"]:
            query.pop("elapsedMs")
    timed = any(q["turnLayerTimeBudgetStops"] or q["boundary"] == "TimeLimit"
                for row in (a, b) for q in row["queries"])
    observed = all(row["queries"] and row["outcome"] in (
        "contract_complete", "first_request_complete", "isolation_complete", "team_win", "team_loss")
        for row in (a, b))
    difference = first_difference(a, b)
    return dict(equalNonTimingData=difference is None, firstDifference=difference,
                timeBoundaryObserved=timed, completedObservations=observed,
                passed=difference is None and not timed and observed)


def pair_summary(pair_id, rows):
    if len(rows) != 2 or {r["arm"] for r in rows} != {"shared_off", "shared_on"}:
        raise ValueError(f"Expected exactly one row per arm: {pair_id}")
    by_arm = {r["arm"]: r for r in rows}
    off, on = by_arm["shared_off"], by_arm["shared_on"]
    for field in ("root", "peer", "scheduleId", "requestCadenceId"):
        if off[field] != on[field]:
            raise ValueError(f"Pair has different {field}: {pair_id}")
    summary = dict(pair=pair_id, root=off["root"], peer=off["peer"],
                   schedule=off["scheduleId"], cadence=off["requestCadenceId"],
                   offOutcome=off["outcome"], onOutcome=on["outcome"],
                   offStatus=off["status"], onStatus=on["status"],
                   bothWon=all(r["status"] == "Passed" and r["outcome"] == "team_win" for r in (off, on)))
    for name, row in (("off", off), ("on", on)):
        if "health" in row:
            summary[name + "Deaths"] = sum(h["deaths"] for h in row["health"])
            summary[name + "Revivals"] = sum(h["revivals"] for h in row["health"])
            summary[name + "Round"] = row["round"]
    if off.get("queries") and on.get("queries"):
        a = load(Path(off["evidence"]) / "multiplayer-experiment.json")
        b = load(Path(on["evidence"]) / "multiplayer-experiment.json")
        qa, qb = a["queries"][0], b["queries"][0]
        pa, pb = copy.deepcopy(qa["policy"]), copy.deepcopy(qb["policy"])
        if pa["Multiplayer"]["creditSharedDamage"] is not False or pb["Multiplayer"]["creditSharedDamage"] is not True:
            raise ValueError(f"Treatment binding mismatch: {pair_id}")
        pa["Multiplayer"]["creditSharedDamage"] = True
        summary.update(firstQueryRootEqual=qa["root"] == qb["root"],
                       firstQuerySolePolicyDifference=first_difference(pa, pb) is None,
                       firstAdviceEqual=qa["advice"] == qb["advice"],
                       offTimeLimitedQueries=off["timeLimitedQueries"],
                       onTimeLimitedQueries=on["timeLimitedQueries"],
                       offQueryCount=off["queryCount"], onQueryCount=on["queryCount"],
                       offInvalidAdvice=off["invalidAdvice"], onInvalidAdvice=on["invalidAdvice"])
        local_a = [action_signature(e["detail"]["action"]) for e in a["events"]
                   if e["kind"] == "action_after" and e["detail"]["actor"] == 1]
        local_b = [action_signature(e["detail"]["action"]) for e in b["events"]
                   if e["kind"] == "action_after" and e["detail"]["actor"] == 1]
        summary["localActionTapeEqual"] = local_a == local_b
        divergence = next((i for i, (x, y) in enumerate(zip(local_a, local_b)) if x != y), None)
        if divergence is None and len(local_a) != len(local_b):
            divergence = min(len(local_a), len(local_b))
        if divergence is not None:
            summary["firstLocalDivergence"] = dict(index=divergence,
                off=local_a[divergence] if divergence < len(local_a) else None,
                on=local_b[divergence] if divergence < len(local_b) else None)
        summary["observedUnattributedDamage"] = {
            arm: max((q["objective"]["observedUnattributedDamage"] for q in row["queries"]), default=0)
            for arm, row in (("off", off), ("on", on))}
        summary["maximumEvaluatedUnattributedDamage"] = {
            arm: max((q["maximumObservedUnattributedDamage"] for q in row["queries"]), default=0)
            for arm, row in (("off", off), ("on", on))}
    if summary["bothWon"]:
        if not summary.get("firstQueryRootEqual") or not summary.get("firstQuerySolePolicyDifference"):
            raise ValueError(f"Cannot compare winning health without the paired root/policy contract: {pair_id}")
        hp_off = {h["actor"]: h for h in off["health"]}
        hp_on = {h["actor"]: h for h in on["health"]}
        summary.update(localHpLossOff=hp_off[1]["combatHpLost"], localHpLossOn=hp_on[1]["combatHpLost"],
                       localHpLossDelta=hp_on[1]["combatHpLost"] - hp_off[1]["combatHpLost"],
                       teamHpLossOff=sum(h["combatHpLost"] for h in hp_off.values()),
                       teamHpLossOn=sum(h["combatHpLost"] for h in hp_on.values()))
        summary["teamHpLossDelta"] = summary["teamHpLossOn"] - summary["teamHpLossOff"]
    return summary


def win_bounds(rows):
    wins = sum(r["status"] == "Passed" and r["outcome"] == "team_win" for r in rows)
    losses = sum(r["status"] == "Passed" and r["outcome"] == "team_loss" for r in rows)
    unknown = len(rows) - wins - losses
    return dict(registered=len(rows), wins=wins, losses=losses, unknown=unknown,
                winCountLower=wins, winCountUpper=wins + unknown)


def analyze(results):
    groups = collections.defaultdict(list)
    for row in results:
        groups[row["pair"]].append(row)
    pairs = [pair_summary(key, rows) for key, rows in groups.items()]
    complete = [p for p in pairs if p["bothWon"]]
    checked = [p for p in pairs if "firstQueryRootEqual" in p]
    queries = [q for r in results for q in r.get("queries", [])]
    summary = dict(plannedTrajectories=len(results), plannedPairs=len(pairs),
                   statusCounts=dict(collections.Counter(r["status"] for r in results)),
                   outcomeCounts=dict(collections.Counter(r["outcome"] for r in results)),
                   bothWonPairs=len(complete), humanSources=0, independentTemplateGroups=1,
                   nativeQueries=sum(r.get("queryCount", 0) for r in results),
                   comparableStageOrPredictedTerminalQueries=sum(r.get("stageCovered", 0) for r in results),
                   timeLimitedQueries=sum(r.get("timeLimitedQueries", 0) for r in results),
                   invalidAdvice=sum(r.get("invalidAdvice", 0) for r in results),
                   firstQueryPairsChecked=len(checked),
                   firstQueryPairsUnavailable=len(pairs) - len(checked),
                   firstQueryPairChecksPassed=all(p["firstQueryRootEqual"] and p["firstQuerySolePolicyDifference"]
                                                 for p in checked) if checked else None,
                   firstAdviceDifferentPairs=sum(p.get("firstAdviceEqual") is False for p in pairs),
                   localTapeDifferentPairs=sum(p.get("localActionTapeEqual") is False for p in pairs))
    summary["teamWinCountBounds"] = {
        arm: win_bounds([r for r in results if r["arm"] == arm])
        for arm in ("shared_off", "shared_on")}
    bounds_off, bounds_on = summary["teamWinCountBounds"].values()
    summary["pairedWinCountDifferenceBounds"] = dict(
        lower=bounds_on["winCountLower"] - bounds_off["winCountUpper"],
        upper=bounds_on["winCountUpper"] - bounds_off["winCountLower"])
    summary["terminalOutcomePairs"] = dict(collections.Counter(
        p["offOutcome"] + "/" + p["onOutcome"] for p in pairs))
    summary["selectedFacts"] = dict(
        comparable=sum(q["selectedFacts"]["comparable"] for q in queries),
        predictedWon=sum(q["selectedFacts"]["won"] for q in queries),
        predictedDead=sum(q["selectedFacts"]["dead"] for q in queries),
        comparableNonterminalStageCovered=sum(q["stageCovered"] and not q["selectedFacts"]["won"]
                                             and not q["selectedFacts"]["dead"] for q in queries),
        actualScoreFallbackCount=None)
    summary["unavailableArms"] = [
        {k: r[k] for k in ("label", "root", "arm", "status", "outcome")}
        for r in results if r["status"] != "Passed" or r["outcome"] not in ("team_win", "team_loss")]
    for metric in ("localHpLossDelta", "teamHpLossDelta"):
        values = [p[metric] for p in complete]
        summary[metric] = dict(improved=sum(v < 0 for v in values), unchanged=sum(v == 0 for v in values),
                               worsened=sum(v > 0 for v in values), minimum=min(values, default=None),
                               maximum=max(values, default=None), total=sum(values))
    summary["byRoot"] = []
    for root in dict.fromkeys(r["root"] for r in results):
        subset = [p for p in complete if p["root"] == root]
        summary["byRoot"].append(dict(root=root, bothWonPairs=len(subset),
                                     localDeltaSum=sum(p["localHpLossDelta"] for p in subset),
                                     teamDeltaSum=sum(p["teamHpLossDelta"] for p in subset)))
    for field in ("peer", "schedule", "cadence"):
        summary["by" + field.title()] = [
            dict(value=value, bothWonPairs=len(subset),
                 localDeltaSum=sum(p["localHpLossDelta"] for p in subset),
                 teamDeltaSum=sum(p["teamHpLossDelta"] for p in subset))
            for value in dict.fromkeys(p[field] for p in pairs)
            for subset in [[p for p in complete if p[field] == value]]]
    return dict(summary=summary, pairs=pairs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", type=Path)
    parser.add_argument("--aa", nargs=2, type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if bool(args.results) == bool(args.aa):
        parser.error("Choose --results or --aa")
    data = analyze(load(args.results)) if args.results else aa(*args.aa)
    args.output.write_text(json.dumps(data, indent=2) + "\n")
    print(json.dumps(data.get("summary", data), indent=2))


if __name__ == "__main__":
    main()
