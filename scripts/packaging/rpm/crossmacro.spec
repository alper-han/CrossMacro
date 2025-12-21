Name:           crossmacro
Version:        %{version}
Release:        1%{?dist}
Summary:        Mouse and Keyboard Macro Automation Tool

License:        GPL-3.0
URL:            https://github.com/alper-han/CrossMacro
Source0:        %{name}-%{version}.tar.gz
Source1:        99-crossmacro.rules
Source2:        crossmacro.te

BuildArch:      x86_64
AutoReqProv:    no
Requires:       glibc, libstdc++, polkit
BuildRequires:  checkpolicy, semodule-utils, systemd-rpm-macros

%{?systemd_requires}

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

# Copy UI
cp -r %{_sourcedir}/publish/* %{buildroot}/usr/lib/%{name}/

# Copy Daemon
cp -r %{_sourcedir}/daemon/* %{buildroot}/usr/lib/%{name}/daemon/

# Copy Service (already has correct ExecStart path)
cp %{_sourcedir}/crossmacro.service %{buildroot}/usr/lib/systemd/system/crossmacro.service
install -m 0644 %{_sourcedir}/99-crossmacro.rules %{buildroot}/usr/lib/udev/rules.d/99-crossmacro.rules
install -m 0644 crossmacro.pp %{buildroot}/usr/share/selinux/packages/%{name}/crossmacro.pp
install -m 0644 %{_sourcedir}/org.crossmacro.policy %{buildroot}/usr/share/polkit-1/actions/org.crossmacro.policy

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
%systemd_post crossmacro.service
udevadm control --reload-rules && udevadm trigger >/dev/null 2>&1 || :
semodule -i /usr/share/selinux/packages/%{name}/crossmacro.pp >/dev/null 2>&1 || :
echo "CrossMacro installed. To use the daemon, add yourself to the 'crossmacro' group:"
echo "sudo usermod -aG crossmacro \$USER"

%preun
%systemd_preun crossmacro.service

%postun
%systemd_postun_with_restart crossmacro.service
if [ $1 -eq 0 ]; then
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
/usr/share/polkit-1/actions/org.crossmacro.policy

%changelog
* Sat Dec 21 2025 Zynix <crossmacro@zynix.net> - 0.6.0-1
- Version 0.6.0 release
