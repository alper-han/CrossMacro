#!/usr/bin/env bash
set -euo pipefail

APP_ID="io.github.alper_han.crossmacro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_SMOKE="$SCRIPT_DIR/cli-smoke.sh"

usage() {
  cat <<'USAGE'
Usage: flatpak.sh <bundle.flatpak> [--installation <name>] [--no-cli]

Validates a CrossMacro Flatpak bundle using user/local install mechanics:
  - verifies the .flatpak bundle exists
  - installs the bundle with flatpak --user into the selected installation
  - runs shared CLI smoke through: flatpak run io.github.alper_han.crossmacro

Options:
  --installation <name>  Flatpak installation name (default: user)
  --no-cli               Install and inspect only; skip executable CLI smoke
  -h, --help             Show this help
USAGE
}

fail() {
  echo "Flatpak smoke failed: $1" >&2
  if [ "$#" -gt 1 ] && [ -n "$2" ]; then
    echo "$2" >&2
  fi
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

bundle=""
installation="user"
skip_cli=0
installed=0
cleanup() {
  if [ "$installed" -eq 1 ]; then
    if [ "$installation" = "user" ]; then
      flatpak --user uninstall -y "$APP_ID" >/dev/null 2>&1 || true
    else
      flatpak --installation="$installation" uninstall -y "$APP_ID" >/dev/null 2>&1 || true
    fi
  fi
}
trap cleanup EXIT

while [ "$#" -gt 0 ]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --installation)
      [ "$#" -ge 2 ] || fail "--installation requires a value"
      installation="$2"
      shift 2
      ;;
    --no-cli)
      skip_cli=1
      shift
      ;;
    --*)
      fail "unknown option: $1"
      ;;
    *)
      [ -z "$bundle" ] || fail "only one .flatpak path may be provided"
      bundle="$1"
      shift
      ;;
  esac
done

[ -n "$bundle" ] || fail "missing .flatpak artifact path"
[ -f "$bundle" ] || fail "missing .flatpak artifact: $bundle"
require_command flatpak
[ -x "$CLI_SMOKE" ] || fail "shared CLI smoke helper not executable: $CLI_SMOKE"

if [ "$installation" = "user" ]; then
  flatpak --user install -y --noninteractive "$bundle"
  installed=1
  run_prefix=(flatpak run "$APP_ID")
  permissions="$(flatpak --user info --show-permissions "$APP_ID")"
else
  flatpak --installation="$installation" install -y --noninteractive "$bundle"
  installed=1
  run_prefix=(flatpak --installation="$installation" run "$APP_ID")
  permissions="$(flatpak --installation="$installation" info --show-permissions "$APP_ID")"
fi

printf '%s\n' "$permissions" | grep -Fq '/run/crossmacro' && fail "portable package exposes the host daemon socket"

if [ "$skip_cli" -eq 0 ]; then
  "$CLI_SMOKE" -- "${run_prefix[@]}"

  if [ "${CROSSMACRO_FLATPAK_SHELL_SMOKE_TESTS:-0}" != "1" ]; then
    echo "Flatpak smoke: nested shell isolation skipped; set CROSSMACRO_FLATPAK_SHELL_SMOKE_TESTS=1 in a desktop session to enable it."
    exit 0
  fi

  shell_output="$("${run_prefix[@]}" run --step 'shell capture "printf sandbox-ok" shell_code shell_out shell_err' --json 2>&1)" ||
    fail "nested sandbox shell execution failed" "$shell_output"
  printf '%s\n' "$shell_output" | grep -Fq '"shell_code": "0"' || fail "nested sandbox shell exit code mismatch" "$shell_output"
  printf '%s\n' "$shell_output" | grep -Fq '"shell_out": "sandbox-ok"' || fail "nested sandbox shell output mismatch" "$shell_output"

  boundary_output="$("${run_prefix[@]}" run --step 'shell capture "flatpak-spawn --host /usr/bin/true" escape_code escape_out escape_err' --step 'shell capture "test ! -e /dev/uinput && test ! -d /dev/input && ! env | grep -q DBUS_SESSION_BUS_ADDRESS" boundary_code boundary_out boundary_err' --json 2>&1)" ||
    fail "nested sandbox boundary check failed" "$boundary_output"
  printf '%s\n' "$boundary_output" | grep -Fq '"escape_code": "0"' && fail "nested shell reached the host command channel" "$boundary_output"
  printf '%s\n' "$boundary_output" | grep -Fq '"boundary_code": "0"' || fail "nested shell inherited device or session-bus access" "$boundary_output"
fi

echo "Flatpak smoke: OK"
