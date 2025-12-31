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
  groupadd -r crossmacro
fi

if ! id "crossmacro" &>/dev/null; then
  echo "   Creating system user 'crossmacro'..."
  useradd -r -s /bin/false -g input -G crossmacro crossmacro
fi

# Ensure daemon user is in both required groups
usermod -aG input crossmacro 2>/dev/null || echo "   Warning: Failed to add to input group"
usermod -aG crossmacro crossmacro 2>/dev/null || echo "   Warning: Failed to add to crossmacro group"


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
# 3. Install Polkit & Udev Rules
# -----------------------------------------------------------------------------
echo ""
echo "Installing Polkit & Udev rules..."


# Install UDev Rules (Permissions for /dev/uinput)
# This allows the 'input' group to write to /dev/uinput
if [ -d "/etc/udev/rules.d" ]; then
    cp "$REPO_ROOT/scripts/assets/99-crossmacro.rules" /etc/udev/rules.d/99-crossmacro.rules
    echo "   Installed udev rules to /etc/udev/rules.d/"
else
    echo "   Warning: /etc/udev/rules.d not found. Manual permission setup for /dev/uinput might be needed."
fi

# Install Modules Load Config (Load uinput on boot)
if [ -d "/etc/modules-load.d" ]; then
    if [ -f "$REPO_ROOT/scripts/assets/crossmacro-modules.conf" ]; then
        cp "$REPO_ROOT/scripts/assets/crossmacro-modules.conf" /etc/modules-load.d/crossmacro.conf
        echo "   Installed modules-load config to /etc/modules-load.d/"
    else 
        echo "   Warning: scripts/assets/crossmacro-modules.conf not found. Skipping."
    fi
else
    echo "   Warning: /etc/modules-load.d not found. You may need to load 'uinput' manually."
fi

# Install Policy (Action Definitions)
# This allows the "org.crossmacro.input-capture" actions to be known to the system.
cp "$REPO_ROOT/scripts/assets/org.crossmacro.policy" /usr/share/polkit-1/actions/org.crossmacro.policy
chmod 644 /usr/share/polkit-1/actions/org.crossmacro.policy


# Install Rules (Authorization Logic)
# This allows the 'crossmacro' group members to bypass password prompt.
# Note: Debian-based systems use .pkla (LocalAuthority), but newer ones and Arch use .rules (JavaScript).
# We install the .rules file. If the system is old (pre-2012 polkit), this might be ignored, 
# but most modern distros (Ubuntu 20.04+, Arch, Fedora) use .rules.
if [ -d "/etc/polkit-1/rules.d" ]; then
    cp "$REPO_ROOT/scripts/assets/50-crossmacro.rules" /etc/polkit-1/rules.d/50-crossmacro.rules
    chmod 644 /etc/polkit-1/rules.d/50-crossmacro.rules
    echo "   Installed JS rules to /etc/polkit-1/rules.d/"
else
    echo "   Warning: /etc/polkit-1/rules.d not found. Polkit auto-auth might not work."
fi

# Restart polkit to extract changes immediately (though it usually watches files)
if systemctl is-active --quiet polkit; then
    systemctl restart polkit || true
fi

# Reload udev rules to apply permissions immediately
echo "   Reloading udev rules..."
udevadm control --reload-rules && udevadm trigger

# Load uinput module immediately
echo "   Loading uinput kernel module..."
modprobe uinput || echo "   Warning: Failed to load uinput module. Reboot may be required."

# -----------------------------------------------------------------------------
# 4. Install systemd service
# -----------------------------------------------------------------------------
echo ""
echo "Installing systemd service..."

cp "$SCRIPT_DIR/crossmacro.service" /etc/systemd/system/crossmacro.service
# Fix path for manual /opt install
sed -i "s|ExecStart=/usr/lib/crossmacro/daemon/CrossMacro.Daemon|ExecStart=$INSTALL_DIR/CrossMacro.Daemon|g" /etc/systemd/system/crossmacro.service
systemctl daemon-reload
systemctl enable crossmacro.service
systemctl restart crossmacro.service

# -----------------------------------------------------------------------------
# 5. Done
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

