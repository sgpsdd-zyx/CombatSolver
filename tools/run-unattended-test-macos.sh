#!/bin/zsh
# Run native request JSON files in one isolated macOS game process.
# Usage: <request.json> [...] --cleanup-instance-on-exit [--timeout-seconds 120] [--output-dir path]
# COMBATSOLVER_STS2_APP / COMBATSOLVER_RITSU_DIR override the installed inputs.
# COMBATSOLVER_HEADLESS_ROOT selects an exact, previously nonexistent instance directory.
set -euo pipefail
setopt nullglob

repo="$(cd "$(dirname "$0")/.." && pwd)"
requests=()
timeout_sec=120
cleanup_requested=0
output_dir=""
while (( $# )); do
    case "$1" in
        --cleanup-instance-on-exit) cleanup_requested=1; shift ;;
        --timeout-seconds) timeout_sec="$2"; shift 2 ;;
        --output-dir) output_dir="$2"; shift 2 ;;
        --*) print -u2 "Unknown option: $1"; exit 2 ;;
        *) requests+=("${1:A}"); shift ;;
    esac
done
(( ${#requests} > 0 && cleanup_requested )) || {
    print -u2 'Supply request JSON files and --cleanup-instance-on-exit.'; exit 2
}
[[ "$timeout_sec" == <1-120> ]] || { print -u2 'Timeout must be 1-120 seconds.'; exit 2; }
for request_src in "${requests[@]}"; do
    [[ -f "$request_src" ]] || { print -u2 "Missing request: $request_src"; exit 2; }
done
steam_root="$HOME/Library/Application Support/Steam/steamapps"
src_app="${COMBATSOLVER_STS2_APP:-$steam_root/common/Slay the Spire 2/SlayTheSpire2.app}"
ritsu_dir="${COMBATSOLVER_RITSU_DIR:-$steam_root/workshop/content/2868840/3747602295}"
dll="$repo/.godot/mono/temp/bin/Release/CombatSolver.dll"
[[ -f "$dll" && -x "$src_app/Contents/MacOS/Slay the Spire 2" && -f "$ritsu_dir/mod_manifest.json" ]] || {
    print -u2 'Build CombatSolver and configure the game app and RitsuLib inputs first.'; exit 2
}
output_dir="${output_dir:-$repo/.local/unattended/macos-$(date +%Y%m%d-%H%M%S)-$$}"
output_dir="${output_dir:A}"
mkdir -p "$output_dir"
if [[ -n "${COMBATSOLVER_HEADLESS_ROOT:-}" ]]; then
    instance_dir="${COMBATSOLVER_HEADLESS_ROOT:A}"
    [[ ! -e "$instance_dir" ]] || { print -u2 "Instance already exists: $instance_dir"; exit 2; }
    mkdir -p "${instance_dir:h}"
    mkdir "$instance_dir"
else
    mkdir -p "$repo/.local/headless-instances"
    instance_dir="$(mktemp -d "$repo/.local/headless-instances/macos.XXXXXX")"
fi
game_pid=""
cleanup() {
    if [[ -n "$game_pid" ]]; then
        kill "$game_pid" 2>/dev/null || true
        for attempt in {1..20}; do
            kill -0 "$game_pid" 2>/dev/null || break
            sleep 0.1
        done
        kill -9 "$game_pid" 2>/dev/null || true
        wait "$game_pid" 2>/dev/null || true
    fi
    for log_file in "$instance_dir"/*.log; do
        cp "$log_file" "$output_dir/"
    done
    rm -rf "$instance_dir"
    print "Instance removed: $instance_dir"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
[[ "$output_dir" != "$instance_dir" && "$output_dir" != "$instance_dir/"* ]] || {
    print -u2 'Output directory must be outside the disposable instance.'; exit 2
}

app="$instance_dir/SlayTheSpire2.app"
profile_root="$instance_dir/profile"
cp -cR "$src_app" "$app"
mods="$app/Contents/MacOS/mods"
rm -rf "$mods"
mkdir -p "$mods/CombatSolver" "$mods/STS2-RitsuLib"
cp "$dll" "$mods/CombatSolver/CombatSolver.dll"
cp "$repo/CombatSolver.json" "$mods/CombatSolver/CombatSolver.json"
cp -R "$ritsu_dir/." "$mods/STS2-RitsuLib/"
cp "$ritsu_dir/mod_manifest.json" "$mods/STS2-RitsuLib/STS2-RitsuLib.json"
rm -f "$mods/STS2-RitsuLib/mod_manifest.json" "$mods/STS2-RitsuLib/RitsuLib.References.props"
data_dir="$profile_root/Library/Application Support/SlayTheSpire2"
mkdir -p "$data_dir/default/1"
# Copy settings only; never copy saves or modify the interactive profile.
python3 - "$data_dir/default/1/settings.save" "$output_dir" "$timeout_sec" "${requests[@]}" <<'PY'
import json, sys, uuid
from pathlib import Path
settings_path, output = Path(sys.argv[1]), Path(sys.argv[2])
interactive = Path.home() / 'Library/Application Support/SlayTheSpire2'
candidates = sorted(interactive.glob('steam/*/settings.save'))
default = interactive / 'default/1/settings.save'
source = default if default.is_file() else next(iter(candidates), None)
settings = json.loads(source.read_text()) if source else {}
settings['mod_settings'] = {'mods_enabled': True, 'mod_list': []}
settings_path.write_text(json.dumps(settings))
requests = sys.argv[4:]
for index, source_path in enumerate(requests, 1):
    request = json.loads(Path(source_path).read_text())
    if request.get('multiplayerExperimentPath'):
        request['multiplayerExperimentPath'] = str(
            (Path(source_path).parent / request['multiplayerExperimentPath']).resolve(strict=True))
    request['runId'] = 'macos-' + uuid.uuid4().hex
    request['exitOnComplete'] = index == len(requests)
    request['timeoutSeconds'] = min(float(request.get('timeoutSeconds', 120)), int(sys.argv[3]))
    request['evidenceDirectory'] = str(output / f'{index:02d}-evidence')
    (output / f'{index:02d}-request.json').write_text(json.dumps(request, indent=2))
PY
cp "$output_dir/01-request.json" "$data_dir/combat_solver_test_request.json"
cd "$app/Contents/MacOS"
env HOME="$profile_root" COMBATSOLVER_HEADLESS=1 "./Slay the Spire 2" \
    --headless --disable-vsync --max-fps 0 --force-steam=off \
    --log-file "$instance_dir/godot-headless.log" >"$instance_dir/launcher.log" 2>&1 &
game_pid=$!
result_path="$data_dir/combat_solver_test_result.json"
for ((index=1; index<=${#requests}; index++)); do
    label="$(printf '%02d' "$index")"
    if (( index > 1 )); then
        rm -f "$result_path"
        cp "$output_dir/$label-request.json" "$data_dir/request.tmp"
        mv "$data_dir/request.tmp" "$data_dir/combat_solver_test_request.json"
    fi
    for ((elapsed=0; elapsed<timeout_sec; elapsed++)); do
        [[ -f "$result_path" ]] && break
        kill -0 "$game_pid" 2>/dev/null || break
        sleep 1
    done
    if [[ ! -f "$result_path" ]]; then
        termination_kind=game_exited
        (( elapsed >= timeout_sec )) && termination_kind=timeout
        python3 - "$output_dir/$label-request.json" "$termination_kind" <<'PY'
import json, sys
from pathlib import Path
request = json.loads(Path(sys.argv[1]).read_text())
evidence = Path(request['evidenceDirectory'])
evidence.mkdir(parents=True, exist_ok=True)
(evidence / 'launcher-result.json').write_text(json.dumps({
    'runId': request['runId'], 'status': sys.argv[2],
    'reason': 'Native process returned no request result.',
}, indent=2) + '\n')
PY
        print -u2 "No result for request $label ($termination_kind); see $output_dir"
        exit 1
    fi
    cp "$result_path" "$output_dir/$label-result.json"
    python3 - "$output_dir/$label-result.json" "$output_dir/$label-request.json" <<'PY'
import json, sys
result = json.load(open(sys.argv[1]))
request = json.load(open(sys.argv[2]))
if result.get('runId') != request['runId']:
    raise SystemExit('Result belongs to a different request.')
print(request.get('scenarioId'), result.get('status'), result.get('error'))
print('Checks:', result.get('completedChecks'))
raise SystemExit(0 if result.get('status') == 'Passed' else 1)
PY
done
