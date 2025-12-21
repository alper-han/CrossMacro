#!/bin/bash
set -e

# =============================================================================
# CrossMacro Daemon Installer
# This script installs the CrossMacro input daemon service.
# Run with: sudo ./scripts/daemon/install.sh (from repo root)
#           or: sudo ./install.sh (from scripts/daemon directory)
# =============================================================================

if [ "$EUID" -ne 0 ]; then
  echo "Error: Please run as root: sudo $0"
  exit 1
fi

# Determine script directory and repo root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Verify we're in the right place
if [ ! -f "$REPO_ROOT/src/CrossMacro.Daemon/CrossMacro.Daemon.csproj" ]; then
    echo "Error: Cannot find CrossMacro.Daemon project."
    echo "   Expected at: $REPO_ROOT/src/CrossMacro.Daemon/"
    echo "   Please run this script from the CrossMacro repository."
    exit 1
fi

echo "Installing CrossMacro Daemon..."
echo "   Repository: $REPO_ROOT"

# -----------------------------------------------------------------------------
# 1. Create group and user
# -----------------------------------------------------------------------------
echo ""
echo "Setting up user and groups..."

if ! getent group crossmacro >/dev/null; then
  echo "   Creating group 'crossmacro'..."
  groupadd crossmacro
fi

if ! id "crossmacro" &>/dev/null; then
  echo "   Creating system user 'crossmacro'..."
  useradd -r -s /bin/false -g crossmacro crossmacro
fi

# Add daemon user to required groups
usermod -aG input crossmacro 2>/dev/null || echo "   Warning: Failed to add to input group"


# Add the installing user to crossmacro group
if [ -n "$SUDO_USER" ]; then
    echo "   Adding '$SUDO_USER' to 'crossmacro' group..."
    usermod -aG crossmacro "$SUDO_USER"
fi

# -----------------------------------------------------------------------------
# 2. Build Daemon
# -----------------------------------------------------------------------------
echo ""
echo "Building Daemon (Native AOT)..."

DAEMON_PROJECT="$REPO_ROOT/src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"
INSTALL_DIR="/opt/crossmacro/daemon"
mkdir -p "$INSTALL_DIR"

if [ -n "$SUDO_USER" ]; then
    # Build as original user to avoid dotnet SDK permission issues
    sudo -u "$SUDO_USER" dotnet publish "$DAEMON_PROJECT" \
        -c Release \
        -o /tmp/crossmacro_daemon_build \
        --verbosity quiet
    
    cp /tmp/crossmacro_daemon_build/CrossMacro.Daemon "$INSTALL_DIR/"
    rm -rf /tmp/crossmacro_daemon_build
else
    dotnet publish "$DAEMON_PROJECT" \
        -c Release \
        -o "$INSTALL_DIR" \
        --verbosity quiet
fi

chmod +x "$INSTALL_DIR/CrossMacro.Daemon"
echo "   Daemon installed to $INSTALL_DIR"

# -----------------------------------------------------------------------------
# 3. Install systemd service
# -----------------------------------------------------------------------------
echo ""
echo "Installing systemd service..."

cp "$SCRIPT_DIR/crossmacro.service" /etc/systemd/system/crossmacro.service
systemctl daemon-reload
systemctl enable crossmacro.service
systemctl restart crossmacro.service

# -----------------------------------------------------------------------------
# 4. Done
# -----------------------------------------------------------------------------
echo ""
echo "------------------------------------------------------------------------------"
echo "CrossMacro Daemon installed successfully!"
echo ""
echo "Service status:"
systemctl status crossmacro.service --no-pager -l 2>/dev/null || true
echo ""
echo "------------------------------------------------------------------------------"
echo ""
echo "Next steps:"
echo "   1. Reboot your system for group changes to take effect."
echo "   2. Run 'crossmacro' or start the UI from your application menu."
echo ""
if [ -n "$SUDO_USER" ]; then
    echo "User '$SUDO_USER' has been added to the 'crossmacro' group."
fi

