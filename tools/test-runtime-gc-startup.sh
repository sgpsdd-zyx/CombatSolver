#!/usr/bin/env bash
# Three fresh native processes sharing one owned Linux game snapshot.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
game=""; ritsu=""; build=""; evidence=""
while (($#)); do
    (($# >= 2)) || { echo 'Each option needs a value.' >&2; exit 2; }
    case "$1" in
        --game-root) game="$2" ;;
        --ritsu-workshop-root) ritsu="$2" ;;
        --build) build="$2" ;;
        --output) evidence="$2" ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
    shift 2
done
[[ -n $game && -n $ritsu && -n $build && -n $evidence ]] || {
    echo 'Required: --game-root --ritsu-workshop-root --build --output' >&2; exit 2
}
[[ -z ${COMBATSOLVER_HEADLESS_ROOT:-} ]] || {
    echo 'Use the repository-owned default instance root.' >&2; exit 2
}
game="$(realpath -e -- "$game")"
ritsu="$(realpath -e -- "$ritsu")"
build="$(realpath -e -- "$build")"
evidence="$(realpath -m -- "$evidence")"
[[ ! -e $evidence ]] || { echo 'Evidence already exists.' >&2; exit 2; }
mkdir -p -- "$evidence"
relative_config='data_sts2_linuxbsd_x86_64/sts2.runtimeconfig.json'
cp -- "$game/$relative_config" "$evidence/original.runtimeconfig.json"
python3 - "$evidence/original.runtimeconfig.json" <<'PY'
import json, sys
with open(sys.argv[1]) as source:
    props = json.load(source)['runtimeOptions'].get('configProperties', {})
if props.get('System.GC.Server') or 'CombatSolver.RuntimeProfile' in props or 'CombatSolver.PreviousServerGc' in props:
    raise SystemExit('This test needs an unmodified WorkstationGC source configuration.')
PY

mkdir -p -- "$repo/.local/headless-instances"
instance_root="$(mktemp -d "$repo/.local/headless-instances/gc-auto.XXXXXX")"
instance="${instance_root##*/}"
launcher="$repo/tools/run-unattended-test.sh"
cleanup() {
    local result=$? cleanup_result=0 log
    trap - EXIT INT TERM
    shopt -s nullglob
    for log in "$instance_root"/*.log; do
        cp -- "$log" "$evidence/diagnostic-${log##*/}" || result=1
    done
    bash "$launcher" --sts2-game-root "$game" --ritsu-workshop-root "$ritsu" \
        --headless-instance "$instance" --stop-instance --cleanup-instance-on-exit || cleanup_result=$?
    if ((cleanup_result != 0)) || [[ -d $instance_root ]]; then
        echo "Owned cleanup failed: $instance_root" >&2
        result=1
    fi
    exit "$result"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
settings="$instance_root/data/SlayTheSpire2/combat_solver_settings.json"
mkdir -p -- "$(dirname "$settings")"
python3 - "$settings" <<'PY'
import json, sys
from pathlib import Path
Path(sys.argv[1]).write_text(json.dumps({
    'performanceMigrationVersion': 246, 'autoConfigureServerGc': True,
    'enableNoGcRegion': True, 'onlineStatisticsEnabled': False,
}))
PY
export COMBATSOLVER_TEST_AUTO_GC_CONFIG=1
unset DOTNET_gcServer COMPlus_gcServer COMBATSOLVER_RUNTIME_PROFILE
for launch in 0 1 2; do
    out="$evidence/launch-$launch"
    if ((launch == 1)); then
        python3 - "$settings" <<'PY'
import json, sys
from pathlib import Path
path = Path(sys.argv[1])
settings = json.loads(path.read_text())
settings['autoConfigureServerGc'] = False
path.write_text(json.dumps(settings))
PY
    fi
    bash "$launcher" --sts2-game-root "$game" --ritsu-workshop-root "$ritsu" \
        --combat-solver-build-dir "$build" --headless-instance "$instance" \
        --runtime-profile default --evidence-directory "$out" \
        --generated-scenario-path "$repo/coverage/runtime-gc-profile/strict-starter.json" \
        --performance-preset-for-test Low --search-budget-override-milliseconds 3000 \
        --search-max-expanded-nodes-for-test 300 --search-max-degree-of-parallelism-for-test 1 \
        --headless-memory-reservation-mib 12288 --headless-cpu-reservation 1 \
        --timeout-seconds 120 --exit-on-complete
    cp -- "$instance_root/game/$relative_config" "$out/next-launch.runtimeconfig.json"
    python3 - "$out" "$launch" "$evidence/original.runtimeconfig.json" <<'PY'
import json, sys
from pathlib import Path
out, launch = Path(sys.argv[1]), int(sys.argv[2])
result = json.loads((out / 'result.json').read_text())
runtime = json.loads((out / 'search-result.json').read_text())['runtime']
active = launch == 1
assert result['status'] == 'Passed'
assert runtime['serverGc'] == active
assert runtime['effectiveNoGcRegionEnabled'] == (not active)
assert runtime['savedNoGcRegionEnabled'] is True
config = json.loads((out / 'next-launch.runtimeconfig.json').read_text())
props = config['runtimeOptions'].get('configProperties', {})
assert (props.get('CombatSolver.RuntimeProfile') == 'server-generational') == (launch == 0)
if launch != 0:
    assert config == json.loads(Path(sys.argv[3]).read_text()), 'Original runtime fields must be restored.'
    assert 'CombatSolver.PreviousServerGc' not in props
(out / 'startup.json').write_text(json.dumps({
    'launch': launch, 'runtime': runtime, 'nextLaunchPrepared': launch == 0, 'status': 'Passed',
}, indent=2) + '\n')
print('GC startup launch', launch, 'Passed; actual ServerGC:', active)
PY
    shopt -s nullglob
    for log in "$instance_root"/*.log; do cp -- "$log" "$out/"; done
done
cmp -s -- "$game/$relative_config" "$evidence/original.runtimeconfig.json" || {
    echo 'Original game runtime configuration changed.' >&2; exit 1
}
echo 'GC startup sequence passed; original game runtime configuration unchanged.'
