#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: cli-smoke.sh (--binary <path> | --command <command> | -- <command> [args...])

Runs the shared CrossMacro CLI smoke contract:
  - top-level --help contains "Usage:"
  - settings get --json contains "status": "ok" and "code": 0
  - dry-run macro JSON contains "coordinateMode": "absolute"

Examples:
  cli-smoke.sh --binary /usr/bin/crossmacro
  cli-smoke.sh --command 'APPIMAGE_EXTRACT_AND_RUN=1 ./CrossMacro.AppImage'
  cli-smoke.sh -- flatpak run io.github.alper_han.crossmacro
USAGE
}

fail() {
  echo "CLI smoke failed: $1" >&2
  if [ "$#" -gt 1 ] && [ -n "$2" ]; then
    echo "$2" >&2
  fi
  exit 1
}

run_command() {
  local output
  local exit_code=0

  if [ "${USE_SHELL_COMMAND:-0}" = "1" ]; then
    output="$(sh -c "$CLI_COMMAND \"\$@\"" cli-smoke-sh "$@" 2>&1)" || exit_code=$?
  else
    output="$(${CLI_COMMAND[@]} "$@" 2>&1)" || exit_code=$?
  fi

  if [ "$exit_code" -ne 0 ]; then
    fail "command exited ${exit_code}: ${COMMAND_DISPLAY} $*" "$output"
  fi

  printf '%s\n' "$output"
}

assert_contains() {
  local assertion_name="$1"
  local haystack="$2"
  local needle="$3"

  printf '%s\n' "$haystack" | grep -F "$needle" >/dev/null || fail "$assertion_name" "$haystack"
}

if [ "$#" -eq 0 ]; then
  usage >&2
  exit 2
fi

USE_SHELL_COMMAND=0
COMMAND_DISPLAY=

while [ "$#" -gt 0 ]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --binary)
      [ "$#" -ge 2 ] || fail "--binary requires a path"
      CLI_COMMAND=("$2")
      COMMAND_DISPLAY="$2"
      shift 2
      ;;
    --command)
      [ "$#" -ge 2 ] || fail "--command requires a command string"
      USE_SHELL_COMMAND=1
      CLI_COMMAND="$2"
      COMMAND_DISPLAY="$2"
      shift 2
      ;;
    --)
      shift
      [ "$#" -gt 0 ] || fail "-- requires a command"
      CLI_COMMAND=("$@")
      COMMAND_DISPLAY="$*"
      shift "$#"
      ;;
    *)
      fail "unknown argument: $1"
      ;;
  esac

  if [ -n "$COMMAND_DISPLAY" ] && [ "$#" -gt 0 ]; then
    fail "only one CLI command target may be provided"
  fi
done

[ -n "$COMMAND_DISPLAY" ] || fail "missing CLI command target"

help_output="$(run_command --help)"
assert_contains 'help Usage:' "$help_output" 'Usage:'

settings_output="$(run_command settings get --json)"
assert_contains 'settings status/code: "status": "ok"' "$settings_output" '"status": "ok"'
assert_contains 'settings status/code: "code": 0' "$settings_output" '"code": 0'

dry_run_output="$(run_command run --step "move abs 10 10" --step "click left" --dry-run --json)"
assert_contains 'dry-run coordinateMode: "coordinateMode": "absolute"' "$dry_run_output" '"coordinateMode": "absolute"'

echo "CLI smoke: OK"
