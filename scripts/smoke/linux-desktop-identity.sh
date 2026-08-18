#!/usr/bin/env bash

crossmacro_validate_native_desktop_identity() {
  local root="$1"
  local executable="$root/usr/lib/crossmacro/CrossMacro.UI"
  local launcher="$root/usr/bin/crossmacro"
  local desktop="$root/usr/share/applications/CrossMacro.desktop"

  command -v file >/dev/null 2>&1 || { echo "file is required for desktop identity validation" >&2; return 1; }
  [ -x "$executable" ] || { echo "native GUI executable is missing: $executable" >&2; return 1; }
  file -L "$executable" | grep -q 'ELF' \
    || { echo "desktop Exec target is not an ELF executable: $executable" >&2; return 1; }
  [ -L "$launcher" ] || { echo "native CLI launcher is not a symlink: $launcher" >&2; return 1; }
  [ "$(readlink "$launcher")" = "/usr/lib/crossmacro/CrossMacro.UI" ] \
    || { echo "native CLI launcher does not target the GUI executable" >&2; return 1; }
  [ -f "$desktop" ] || { echo "native desktop entry is missing: $desktop" >&2; return 1; }
  grep -Fxq 'Exec=/usr/lib/crossmacro/CrossMacro.UI' "$desktop" \
    || { echo "desktop Exec does not target the native GUI executable" >&2; return 1; }
  grep -Fxq 'X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2' "$desktop" \
    || { echo "desktop entry does not declare the KWin ScreenShot2 permission" >&2; return 1; }
}
