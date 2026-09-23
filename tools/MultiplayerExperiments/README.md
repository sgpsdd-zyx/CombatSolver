# Native multiplayer experiments

This tool measures one local advisor user alongside an independently controlled
teammate. The test driver calls the existing manual request path and executes one
native action at a time. The teammate never calls the advisor or reads its plans.

The registered recipes and protocol are in
[the experiment directory](../../docs/strategy/multiplayer-experiments-20260923/README.md).
All generated requests, action/state traces, logs and temporary instances stay in
`.local/`. This is a native headless experiment, not the managed offline search host.

## Apparatus checks

Build the current Release DLL, then run
`coverage/unattended/multiplayer-manual-loop.json` with the native macOS launcher.
On Windows/Linux select `MULTIPLAYER-MANUAL-LOOP` and pass the configuration path
using `-MultiplayerExperimentPath` / `--multiplayer-experiment-path`.
Always include the launcher's instance cleanup flag and a 120-second timeout.

Configuration modes are `Contract` (both actors, next turn, repeated manual
requests, native/simulated action equality and frozen roots), `FirstRequest`,
`RootIsolation` (peer action during a held worker, stale publication and recapture),
and `Pilot` (until native team terminal or the registered cap). `ExpectedOpeningPath`
compares both the full party continuation and normalized native state with an
earlier captured opening; `ExpectedFirstCardId` asserts a frozen sensitivity witness.

`Contract` also exercises native ready withdrawal. It leaves the original local
identity in place. `RootIsolation` is an artificial concurrency ordering contract,
not a timing or quality measurement.

Run `coverage/unattended/multiplayer-experiment-inactive.json` immediately after
the contract in the same process to assert that all experiment hooks/state are
gone and the ordinary solo root/search still work. A deliberately different
`ExpectedOpeningPath` must fail as `root_mismatch` before any query or action.

The request scope emulates the multiplayer advisor gate and all-player ready test
over native single-process transport. It bypasses the first-shuffle tutorial and
the remote-card queue animation whose remote intent widget is absent in that
transport. Native enemy-start acknowledgement remains the single-process path.
Game enum packet-cache entries are initialized on the main thread before starting
the run. None of these measures validates real networking or visible performance.

## Pilot

First admit each recipe by capturing/rebuilding its complete native opening, pass
A/A after an explicitly recorded warmup, and freeze a sensitive positive control.
Create a JSON object mapping each candidate ID to its captured
`multiplayer-opening.json`. Then:

```bash
python3 tools/MultiplayerExperiments/run_native.py \
  --admitted-openings .local/<batch>/admitted-openings.json \
  --output .local/<new-pilot>
python3 tools/MultiplayerExperiments/analyze.py \
  --results .local/<new-pilot>/results.json \
  --output .local/<new-pilot>/analysis.json
```

The complete 64-arm plan is written before execution. On macOS each process runs
an explicit warmup followed by at most four registered trajectories; warmups do not enter
the 64-arm denominator. Other platforms use one native request per invocation and
report time limits, including cold-start effects. Windows/Linux commands are
maintained but require their own runtime validation.

The runner stops on an execution failure and preserves all unexecuted arms. It
never retries a failed trajectory as if it were a new successful sample. Inspect
and fix the boundary, then continue only unexecuted arms in a new directory:

```bash
python3 tools/MultiplayerExperiments/run_native.py \
  --continue-unexecuted-from .local/<stopped-pilot> \
  --output .local/<continuation>
```

The continuation retains the original successful and failed rows and their evidence
paths. An auxiliary rerun of a failed condition must use a separate diagnostic
request; it never replaces that row. `--collect-only` reads existing artifacts
without rerunning a trajectory; use it after the launcher has stopped.
An explicit launcher timeout receipt becomes `request_timeout`; a premature exit
or a missing result without a timeout receipt becomes `execution_error`. A staged
request with no opening or launcher receipt stays `NotExecuted`.

The primary comparison is the same build with only
`Multiplayer.CreditSharedDamage` changed. Subsequent manual roots, cached action
prefixes and teammate choices may diverge naturally after different local actions.
The analysis checks the common first-request policy and root, reports every pair,
keeps unknown outcomes in descriptive win-count bounds,
and compares health only in the explicitly counted both-win subset. Ending HP,
in-combat damage/healing and postcombat recovery remain separate.

The four recipes are one controlled template group, with zero independent human
sources. The two deterministic partner families cover behavior and scheduling
boundaries. Their mixture is not an estimate of human behavior probabilities.

See the [implementation record](../../docs/strategy/multiplayer-experiments-20260923/implementation.md)
for the pilot's failures, observation limits, and which controls actually ran.
