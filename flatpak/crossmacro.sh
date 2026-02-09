#!/bin/sh
# CrossMacro Flatpak Launcher (Hybrid Mode)

export DOTNET_ROOT=/app/lib/dotnet

# Force Avalonia to use Wayland backend when available
if [ -n "$WAYLAND_DISPLAY" ] && [ -z "$DISPLAY" ]; then
    # No X11 available, ensure Avalonia doesn't try X11 first
    export AVALONIA_BACKEND=wayland
fi

# Check if CrossMacro daemon is available on host
DAEMON_SOCKET="/run/crossmacro/crossmacro.sock"

if [ -S "$DAEMON_SOCKET" ]; then
    export CROSSMACRO_USE_DAEMON=1
    echo "[CrossMacro] Using daemon mode (secure)" >&2
else
    export CROSSMACRO_USE_DAEMON=0
    echo "[CrossMacro] Using direct mode (Flatpak permissions)" >&2
fi

exec /app/lib/dotnet/dotnet /app/lib/crossmacro/CrossMacro.UI.dll "$@"
