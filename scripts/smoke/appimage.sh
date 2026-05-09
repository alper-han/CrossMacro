#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_SMOKE="$SCRIPT_DIR/cli-smoke.sh"

usage() {
  cat <<'USAGE'
Usage: appimage.sh <CrossMacro.AppImage> [--no-cli]

Validates a CrossMacro AppImage:
  - verifies the AppImage exists and is executable
  - extracts the AppDir with APPIMAGE_EXTRACT_AND_RUN=1
  - checks AppRun, CrossMacro.UI, crossmacro CLI symlink, desktop metadata, and bundled ICU/native libraries where possible
  - runs shared CLI smoke through an APPIMAGE_EXTRACT_AND_RUN=1 AppImage wrapper unless --no-cli is supplied

Options:
  --no-cli     Skip executable CLI smoke after static/extraction checks
  -h, --help   Show this help
USAGE
}

fail() {
  echo "AppImage smoke failed: $1" >&2
  exit 1
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
      [ -z "$artifact" ] || fail "only one AppImage path may be provided"
      artifact="$1"
      shift
      ;;
  esac
done

[ -n "$artifact" ] || fail "missing AppImage artifact path"
[ -f "$artifact" ] || fail "missing AppImage artifact: $artifact"
[ -x "$artifact" ] || fail "AppImage is not executable: $artifact"
[ -x "$CLI_SMOKE" ] || fail "shared CLI smoke helper not executable: $CLI_SMOKE"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

artifact_abs="$(cd "$(dirname "$artifact")" && pwd)/$(basename "$artifact")"
(
  cd "$work_dir"
  APPIMAGE_EXTRACT_AND_RUN=1 "$artifact_abs" --appimage-extract >/dev/null
)

appdir="$work_dir/squashfs-root"
[ -d "$appdir" ] || fail "AppImage extraction did not create squashfs-root"
[ -x "$appdir/AppRun" ] || fail "extracted AppRun is missing or not executable"
[ -x "$appdir/usr/bin/CrossMacro.UI" ] || fail "extracted CrossMacro.UI is missing or not executable"
[ -e "$appdir/usr/bin/crossmacro" ] || fail "extracted crossmacro CLI link is missing"
[ -f "$appdir/CrossMacro.desktop" ] || [ -f "$appdir/usr/share/applications/CrossMacro.desktop" ] || fail "desktop file is missing"

grep -R "^Exec=AppRun" "$appdir/CrossMacro.desktop" "$appdir/usr/share/applications" >/dev/null 2>&1 || fail "desktop metadata does not use AppRun"

lib_dir="$appdir/usr/lib"
[ -d "$lib_dir" ] || fail "extracted usr/lib directory is missing"
for family in icudata icui18n icuuc; do
  find "$lib_dir" -maxdepth 1 -name "lib${family}.so.*" -print -quit | grep . >/dev/null || fail "bundled lib${family} library not found in usr/lib"
done

icu_versions="$(find "$lib_dir" -maxdepth 1 -type f -name 'libicuuc.so.[0-9]*' -printf '%f\n' \
  | sed -n 's/^libicuuc\.so\.\([0-9][0-9.]*\)$/\1/p' \
  | sort -Vu)"
icu_version_count="$(printf '%s\n' "$icu_versions" | sed '/^$/d' | wc -l | tr -d ' ')"
[ "$icu_version_count" -eq 1 ] || fail "expected exactly one bundled ICU version, found: ${icu_versions:-none}"

icu_version="$icu_versions"
for family in icudata icui18n icuuc; do
  [ -e "$lib_dir/lib${family}.so.$icu_version" ] || fail "bundled lib${family}.so.$icu_version is missing"
done

grep -R "DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU=$icu_version" "$appdir/AppRun" >/dev/null 2>&1 || fail "AppRun does not pin bundled ICU version $icu_version"
if ! find "$lib_dir" -maxdepth 1 -name 'libXtst.so*' -print -quit | grep . >/dev/null; then
  echo "AppImage smoke: libXtst.so not bundled; continuing because build may rely on host library for this architecture." >&2
fi

if [ "$skip_cli" -eq 0 ]; then
  APPIMAGE_EXTRACT_AND_RUN=1 "$CLI_SMOKE" --command "'$artifact_abs'"
fi

echo "AppImage smoke: OK"
