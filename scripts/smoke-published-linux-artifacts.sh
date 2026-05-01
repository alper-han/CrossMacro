#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: smoke-published-linux-artifacts.sh --ui-binary <path>

Smoke-tests published Linux artifacts using the same CLI contract asserted by CI.

Required:
  --ui-binary <path>      Path to published CrossMacro.UI binary.
EOF
}

UI_BINARY=""

require_value() {
    if [ "$#" -lt 2 ] || [ -z "${2:-}" ]; then
        echo "Error: missing value for $1" >&2
        usage >&2
        exit 1
    fi
}

run_and_capture() {
    local __result_var="$1"
    shift

    local output
    local exit_code=0
    output="$("$@" 2>&1)" || exit_code=$?
    if [ "$exit_code" -ne 0 ]; then
        printf '%s\n' "$output"
        echo "Command failed (${exit_code}): $*" >&2
        exit "$exit_code"
    fi

    printf '%s\n' "$output"
    printf -v "$__result_var" '%s' "$output"
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --ui-binary)
            require_value "$@"
            UI_BINARY="$2"
            shift 2
            ;;
        --daemon-binary|--daemon-socket)
            echo "Error: daemon-backed smoke is no longer supported by this helper." >&2
            echo "Use a dedicated Linux integration environment for daemon validation." >&2
            usage >&2
            exit 1
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if [ -z "$UI_BINARY" ]; then
    echo "Error: --ui-binary is required." >&2
    usage >&2
    exit 1
fi

if [ ! -f "$UI_BINARY" ]; then
    echo "Error: UI binary not found: $UI_BINARY" >&2
    exit 1
fi

if [ ! -x "$UI_BINARY" ]; then
    echo "Error: UI binary is not executable: $UI_BINARY" >&2
    exit 1
fi

run_and_capture help_output "$UI_BINARY" --help
echo "$help_output" | grep -F "Usage:"

run_and_capture settings_output "$UI_BINARY" settings get --json
echo "$settings_output" | grep -F '"status": "ok"'
echo "$settings_output" | grep -F '"code": 0'

run_and_capture dry_run_output "$UI_BINARY" run --step "move abs 10 10" --step "click left" --dry-run --json
echo "$dry_run_output" | grep -F '"status": "ok"'
echo "$dry_run_output" | grep -F '"code": 0'
echo "$dry_run_output" | grep -F '"coordinateMode": "absolute"'
