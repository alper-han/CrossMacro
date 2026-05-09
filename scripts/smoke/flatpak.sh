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
  - inspects bundle metadata
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

metadata="$(flatpak info --show-metadata --file "$bundle" 2>/dev/null || flatpak info --file "$bundle")"
printf '%s\n' "$metadata" | grep -F "$APP_ID" >/dev/null || fail "bundle metadata does not mention $APP_ID"

if [ "$installation" = "user" ]; then
  flatpak --user install -y --noninteractive "$bundle"
  installed=1
  run_prefix=(flatpak run "$APP_ID")
else
  flatpak --installation="$installation" install -y --noninteractive "$bundle"
  installed=1
  run_prefix=(flatpak --installation="$installation" run "$APP_ID")
fi

if [ "$skip_cli" -eq 0 ]; then
  "$CLI_SMOKE" -- "${run_prefix[@]}"
fi

echo "Flatpak smoke: OK"
