Name:           crossmacro
Version:        %{version}
Release:        %{?release}%{!?release:1}%{?dist}
Summary:        Mouse and Keyboard Macro Automation Tool

License:        GPL-3.0-only
URL:            https://github.com/alper-han/CrossMacro
Source0:        %{name}-%{version}.tar.gz
Source1:        99-crossmacro.rules
Source2:        crossmacro.te
Source3:        50-crossmacro.rules
Source4:        crossmacro-modules.conf
Source5:        io.github.alper_han.crossmacro.policy
Source6:        crossmacro.1

# Do not strip .NET single-file bundles during RPM post-processing.
# Stripping can corrupt apphost bundles (observed on aarch64 builds).
%global debug_package %{nil}
%global _build_id_links none
%global __strip /bin/true
%global __debug_install_post %{nil}

BuildArch:      %{_target_cpu}
AutoReqProv:    no
Requires:       glibc, libstdc++, polkit, libXtst, zlib, openssl-libs, systemd-libs, libxkbcommon
BuildRequires:  checkpolicy, policycoreutils

Requires(post): systemd
Requires(post): policycoreutils
Requires(preun): systemd
Requires(postun): systemd
Requires(postun): policycoreutils

%description
A powerful cross-platform mouse and keyboard macro automation tool.
Supports text expansion and works on Linux (Wayland/X11), Windows, and macOS.

%prep
# No prep needed as we are using pre-built binaries

%build
# Build SELinux policy
checkmodule -M -m -o crossmacro.mod %{_sourcedir}/crossmacro.te
semodule_package -o crossmacro.pp -m crossmacro.mod

%install
mkdir -p %{buildroot}/usr/lib/%{name}
mkdir -p %{buildroot}/usr/lib/%{name}/daemon
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
# Icons handled in loop
mkdir -p %{buildroot}/usr/lib/systemd/system
mkdir -p %{buildroot}/usr/lib/udev/rules.d
mkdir -p %{buildroot}/usr/share/icons/hicolor
mkdir -p %{buildroot}/usr/share/selinux/packages/%{name}
mkdir -p %{buildroot}/usr/share/polkit-1/actions
mkdir -p %{buildroot}/usr/share/polkit-1/rules.d
mkdir -p %{buildroot}/usr/share/man/man1

# Copy UI
cp -r %{_sourcedir}/publish/* %{buildroot}/usr/lib/%{name}/

# Copy Daemon
cp -r %{_sourcedir}/daemon/* %{buildroot}/usr/lib/%{name}/daemon/

# Copy Service (already has correct ExecStart path)
cp %{_sourcedir}/crossmacro.service %{buildroot}/usr/lib/systemd/system/crossmacro.service
install -m 0644 %{_sourcedir}/99-crossmacro.rules %{buildroot}/usr/lib/udev/rules.d/99-crossmacro.rules
install -m 0644 crossmacro.pp %{buildroot}/usr/share/selinux/packages/%{name}/crossmacro.pp
install -m 0644 %{_sourcedir}/io.github.alper_han.crossmacro.policy %{buildroot}/usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy
install -m 0644 %{_sourcedir}/50-crossmacro.rules %{buildroot}/usr/share/polkit-1/rules.d/50-crossmacro.rules

# Install modules-load config
mkdir -p %{buildroot}/usr/lib/modules-load.d
install -m 0644 %{_sourcedir}/crossmacro-modules.conf %{buildroot}/usr/lib/modules-load.d/crossmacro.conf

ln -s ../lib/%{name}/CrossMacro.UI %{buildroot}/usr/bin/%{name}
# Copy icons
cp -r %{_sourcedir}/icons/* %{buildroot}/usr/share/icons/hicolor/
cp %{_sourcedir}/CrossMacro.desktop %{buildroot}/usr/share/applications/%{name}.desktop
install -m 0644 %{_sourcedir}/crossmacro.1 %{buildroot}/usr/share/man/man1/crossmacro.1

%pre
# Create group and user if they don't exist
getent group crossmacro >/dev/null || groupadd -r crossmacro
getent group input >/dev/null || groupadd -r input
getent group uinput >/dev/null || groupadd -r uinput
getent passwd crossmacro >/dev/null || \
    useradd -r -g input -G crossmacro,uinput -s /sbin/nologin \
    -c "CrossMacro Input Daemon User" crossmacro
usermod -aG input crossmacro || :
usermod -aG uinput crossmacro || :
usermod -aG crossmacro crossmacro || :

%post
# Reload rules before loading uinput so the device node is created with the
# packaged permissions on first load.
udevadm control --reload-rules >/dev/null 2>&1 || :
if command -v modprobe >/dev/null 2>&1; then
    modprobe uinput >/dev/null 2>&1 || :
fi
udevadm trigger >/dev/null 2>&1 || :
udevadm settle >/dev/null 2>&1 || :

selinux_enabled=0
if [ -x /usr/sbin/selinuxenabled ]; then
    /usr/sbin/selinuxenabled && selinux_enabled=1 || :
elif command -v selinuxenabled >/dev/null 2>&1; then
    selinuxenabled && selinux_enabled=1 || :
fi

semodule_cmd=""
if [ -x /usr/sbin/semodule ]; then
    semodule_cmd=/usr/sbin/semodule
elif command -v semodule >/dev/null 2>&1; then
    semodule_cmd="$(command -v semodule)"
fi

policy_ready=1
if [ "$selinux_enabled" -eq 1 ]; then
    if [ -z "$semodule_cmd" ]; then
        echo "CrossMacro: SELinux is enabled but semodule was not found; service will not be started." >&2
        policy_ready=0
    elif "$semodule_cmd" -i /usr/share/selinux/packages/%{name}/crossmacro.pp; then
        if ! "$semodule_cmd" -l | awk '{print $1}' | grep -qx crossmacro; then
            echo "CrossMacro: SELinux policy verification failed; service will not be started." >&2
            policy_ready=0
        fi
    else
        echo "CrossMacro: failed to install SELinux policy; service will not be started." >&2
        policy_ready=0
    fi
fi

# systemd_post equivalent
if [ $1 -eq 1 ] && [ "$policy_ready" -eq 1 ]; then
    systemctl daemon-reload >/dev/null 2>&1 || :
    systemctl enable crossmacro.service >/dev/null 2>&1 || :
    systemctl start crossmacro.service >/dev/null 2>&1 || :
elif [ $1 -eq 1 ] && [ "$policy_ready" -ne 1 ]; then
    echo "CrossMacro: start skipped because the SELinux policy is not active yet." >&2
fi

# Best effort: add invoking user to crossmacro group.
# This works in common sudo/pkexec flows; if no user context is available,
# keep the manual instruction as fallback.
installer_user=""
if [ -n "${SUDO_USER:-}" ] && [ "${SUDO_USER}" != "root" ]; then
    installer_user="${SUDO_USER}"
elif [ -n "${PKEXEC_UID:-}" ] && [ "${PKEXEC_UID}" != "0" ]; then
    installer_user="$(getent passwd "${PKEXEC_UID}" | cut -d: -f1)"
fi

if [ -n "$installer_user" ] && getent passwd "$installer_user" >/dev/null 2>&1; then
    usermod -aG crossmacro "$installer_user" >/dev/null 2>&1 || :
    echo "CrossMacro installed. Added '$installer_user' to 'crossmacro' group."
    echo "Re-login (or reboot) is required for group change to take effect."
else
    echo "CrossMacro installed. To use the daemon, add yourself to the 'crossmacro' group:"
    echo "sudo usermod -aG crossmacro \$USER"
fi

%preun
# systemd_preun equivalent
if [ $1 -eq 0 ]; then
    systemctl stop crossmacro.service >/dev/null 2>&1 || :
    systemctl disable crossmacro.service >/dev/null 2>&1 || :
fi

%postun
# systemd_postun_with_restart equivalent
if [ $1 -ge 1 ]; then
    systemctl try-restart crossmacro.service >/dev/null 2>&1 || :
fi
if [ $1 -eq 0 ]; then
    systemctl daemon-reload >/dev/null 2>&1 || :
    if [ -x /usr/sbin/semodule ]; then
        /usr/sbin/semodule -r crossmacro >/dev/null 2>&1 || :
    elif command -v semodule >/dev/null 2>&1; then
        semodule -r crossmacro >/dev/null 2>&1 || :
    fi
fi

%files

/usr/lib/%{name}
/usr/bin/%{name}
/usr/lib/systemd/system/crossmacro.service
/usr/lib/udev/rules.d/99-crossmacro.rules
/usr/share/applications/%{name}.desktop
/usr/share/icons/hicolor/*/apps/%{name}.png
/usr/share/selinux/packages/%{name}/crossmacro.pp
/usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy
/usr/share/polkit-1/rules.d/50-crossmacro.rules
/usr/lib/modules-load.d/crossmacro.conf
/usr/share/man/man1/crossmacro.1*
