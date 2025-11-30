Name:           crossmacro
Version:        %{version}
Release:        1%{?dist}
Summary:        Mouse Macro Automation Tool for Linux

License:        GPL-3.0
URL:            https://github.com/alper-han/CrossMacro
Source0:        %{name}-%{version}.tar.gz

BuildArch:      x86_64
AutoReqProv:    no

%description
A powerful mouse macro automation tool for Linux Wayland compositors.
Supports Hyprland, KDE Plasma, and GNOME Shell.

%prep
# No prep needed as we are using pre-built binaries

%build
# No build needed as we are using pre-built binaries

%install
mkdir -p %{buildroot}/usr/lib/%{name}
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons/hicolor/256x256/apps

cp -r %{_sourcedir}/publish/* %{buildroot}/usr/lib/%{name}/
ln -s /usr/lib/%{name}/CrossMacro.UI %{buildroot}/usr/bin/%{name}
cp %{_sourcedir}/crossmacro.png %{buildroot}/usr/share/icons/hicolor/256x256/apps/%{name}.png
cp %{_sourcedir}/CrossMacro.desktop %{buildroot}/usr/share/applications/%{name}.desktop

%files
/usr/lib/%{name}
/usr/bin/%{name}
/usr/share/applications/%{name}.desktop
/usr/share/icons/hicolor/256x256/apps/%{name}.png

%changelog
* Fri Nov 29 2024 Zynix <crossmacro@zynix.net> - 1.0.0-1
- Initial release
