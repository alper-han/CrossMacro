Name:           crossmacro
Version:        %{version}
Release:        1%{?dist}
Summary:        Mouse and Keyboard Macro Automation Tool

License:        GPL-3.0
URL:            https://github.com/alper-han/CrossMacro
Source0:        %{name}-%{version}.tar.gz
Source1:        99-crossmacro.rules
Source2:        crossmacro.te
Source3:        50-crossmacro.rules
Source4:        crossmacro-modules.conf
Source5:        io.github.alper_han.crossmacro.policy

BuildArch:      x86_64
AutoReqProv:    no
Requires:       glibc, libstdc++, polkit, libXtst, zlib, openssl-libs, systemd-libs
BuildRequires:  checkpolicy, semodule-utils

Requires(post): systemd
Requires(preun): systemd
Requires(postun): systemd

%description
A powerful cross-platform mouse and keyboard macro automation tool.
Supports text expansion and works on Linux (Wayland/X11) and Windows.

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

ln -s /usr/lib/%{name}/CrossMacro.UI %{buildroot}/usr/bin/%{name}
# Copy icons
cp -r %{_sourcedir}/icons/* %{buildroot}/usr/share/icons/hicolor/
cp %{_sourcedir}/CrossMacro.desktop %{buildroot}/usr/share/applications/%{name}.desktop

%pre
# Create group and user if they don't exist
getent group crossmacro >/dev/null || groupadd -r crossmacro
getent passwd crossmacro >/dev/null || \
    useradd -r -g input -G crossmacro -s /sbin/nologin \
    -c "CrossMacro Input Daemon User" crossmacro
# Ensure groups
usermod -aG input crossmacro
usermod -aG crossmacro crossmacro

%post
# systemd_post equivalent
if [ $1 -eq 1 ]; then
    systemctl daemon-reload >/dev/null 2>&1 || :
    systemctl enable crossmacro.service >/dev/null 2>&1 || :
    systemctl start crossmacro.service >/dev/null 2>&1 || :
fi
udevadm control --reload-rules && udevadm trigger >/dev/null 2>&1 || :
semodule -i /usr/share/selinux/packages/%{name}/crossmacro.pp >/dev/null 2>&1 || :
echo "CrossMacro installed. To use the daemon, add yourself to the 'crossmacro' group:"
echo "sudo usermod -aG crossmacro \$USER"

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
    semodule -r crossmacro >/dev/null 2>&1 || :
fi

%files

/usr/lib/%{name}
%attr(0755, root, root) /usr/bin/%{name}
%attr(0755, root, root) /usr/lib/%{name}/CrossMacro.UI
%attr(0755, root, root) /usr/lib/%{name}/daemon/CrossMacro.Daemon
/usr/lib/systemd/system/crossmacro.service
/usr/lib/udev/rules.d/99-crossmacro.rules
/usr/share/applications/%{name}.desktop
/usr/share/icons/hicolor/*/apps/%{name}.png
/usr/share/selinux/packages/%{name}/crossmacro.pp
/usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy
/usr/share/polkit-1/rules.d/50-crossmacro.rules
/usr/lib/modules-load.d/crossmacro.conf

%changelog
* Sat Dec 21 2025 Zynix <crossmacro@zynix.net> - 0.6.0-1
- Version 0.6.0 release
