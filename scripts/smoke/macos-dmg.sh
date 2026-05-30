#!/usr/bin/env bash
set -euo pipefail

APP_NAME="CrossMacro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_SMOKE="$SCRIPT_DIR/cli-smoke.sh"

usage() {
  cat <<'USAGE'
Usage: macos-dmg.sh <CrossMacro.dmg> [--no-cli]

Validates a CrossMacro macOS DMG:
  - verifies the DMG exists
  - runs hdiutil verify
  - mounts the DMG at a temporary mount point
  - validates CrossMacro.app, bundled executable, Info.plist version fields, and app metadata
  - detaches the DMG via trap
  - runs shared CLI smoke on CrossMacro.app/Contents/MacOS/CrossMacro.UI where practical

Options:
  --no-cli     Skip executable CLI smoke after bundle checks
  -h, --help   Show this help
USAGE
}

fail() {
  echo "macOS DMG smoke failed: $1" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

artifact=""
skip_cli=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --no-cli)
      skip_cli=1
      shift
      ;;
    --*)
      fail "unknown option: $1"
      ;;
    *)
      [ -z "$artifact" ] || fail "only one DMG path may be provided"
      artifact="$1"
      shift
      ;;
  esac
done

[ -n "$artifact" ] || fail "missing DMG artifact path"
[ -f "$artifact" ] || fail "missing DMG artifact: $artifact"
require_command hdiutil
[ -x "$CLI_SMOKE" ] || fail "shared CLI smoke helper not executable: $CLI_SMOKE"

hdiutil verify "$artifact"

mount_dir="$(mktemp -d)"
mounted=0
cleanup() {
  if [ "$mounted" -eq 1 ]; then
    hdiutil detach "$mount_dir" -quiet || true
  fi
  rmdir "$mount_dir" 2>/dev/null || true
}
trap cleanup EXIT

hdiutil attach "$artifact" -mountpoint "$mount_dir" -nobrowse -quiet
mounted=1

app_bundle="$mount_dir/$APP_NAME.app"
executable="$app_bundle/Contents/MacOS/CrossMacro.UI"
plist="$app_bundle/Contents/Info.plist"

[ -d "$app_bundle" ] || fail "CrossMacro.app not found in DMG"
[ -x "$executable" ] || fail "bundled executable missing or not executable: $executable"
[ -f "$plist" ] || fail "Info.plist missing: $plist"

grep -A1 '<key>CFBundleIdentifier</key>' "$plist" | grep -F '<string>net.crossmacro.CrossMacro</string>' >/dev/null || fail "Info.plist bundle identifier mismatch"
grep -A1 '<key>CFBundleExecutable</key>' "$plist" | grep -F '<string>CrossMacro.UI</string>' >/dev/null || fail "Info.plist executable mismatch"
grep -F '<key>CFBundleVersion</key>' "$plist" >/dev/null || fail "Info.plist CFBundleVersion missing"
grep -F '<key>CFBundleShortVersionString</key>' "$plist" >/dev/null || fail "Info.plist CFBundleShortVersionString missing"
grep -F '<key>NSInputMonitoringUsageDescription</key>' "$plist" >/dev/null || fail "Info.plist NSInputMonitoringUsageDescription missing"

if [ "$skip_cli" -eq 0 ]; then
  "$CLI_SMOKE" --binary "$executable"
fi

echo "macOS DMG smoke: OK"
