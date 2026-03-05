#!/usr/bin/env bash

set -euo pipefail

normalize_arch() {
    local arch="${1:-}"
    case "$arch" in
        x86_64|amd64|x64)
            echo "x86_64"
            ;;
        aarch64|arm64)
            echo "aarch64"
            ;;
        *)
            echo "$arch"
            ;;
    esac
}

arch_from_rid() {
    local rid="${1:-}"
    local suffix
    suffix="${rid##*-}"
    case "$suffix" in
        x64)
            echo "x86_64"
            ;;
        arm64)
            echo "aarch64"
            ;;
        *)
            echo "$suffix"
            ;;
    esac
}

get_target_arch() {
    if [ -n "${TARGET_ARCH:-}" ]; then
        normalize_arch "$TARGET_ARCH"
        return 0
    fi

    if [ -n "${RID:-}" ]; then
        normalize_arch "$(arch_from_rid "$RID")"
        return 0
    fi

    normalize_arch "$(uname -m)"
}

to_deb_arch() {
    local arch
    arch="$(normalize_arch "${1:-$(get_target_arch)}")"
    case "$arch" in
        x86_64)
            echo "amd64"
            ;;
        aarch64)
            echo "arm64"
            ;;
        *)
            echo "Error: Unsupported Debian architecture '$arch'." >&2
            return 1
            ;;
    esac
}

to_rpm_arch() {
    local arch
    arch="$(normalize_arch "${1:-$(get_target_arch)}")"
    case "$arch" in
        x86_64|aarch64)
            echo "$arch"
            ;;
        *)
            echo "Error: Unsupported RPM architecture '$arch'." >&2
            return 1
            ;;
    esac
}

to_appimage_arch() {
    local arch
    arch="$(normalize_arch "${1:-$(get_target_arch)}")"
    case "$arch" in
        x86_64|aarch64)
            echo "$arch"
            ;;
        *)
            echo "Error: Unsupported AppImage architecture '$arch'." >&2
            return 1
            ;;
    esac
}

to_flatpak_arch() {
    local arch
    arch="$(normalize_arch "${1:-$(get_target_arch)}")"
    case "$arch" in
        x86_64|aarch64)
            echo "$arch"
            ;;
        *)
            echo "Error: Unsupported Flatpak architecture '$arch'." >&2
            return 1
            ;;
    esac
}

to_dotnet_arch() {
    local arch
    arch="$(normalize_arch "${1:-$(get_target_arch)}")"
    case "$arch" in
        x86_64)
            echo "x64"
            ;;
        aarch64)
            echo "arm64"
            ;;
        *)
            echo "Error: Unsupported .NET architecture '$arch'." >&2
            return 1
            ;;
    esac
}

get_glibc_interpreter() {
    local arch
    arch="$(normalize_arch "${1:-$(get_target_arch)}")"
    case "$arch" in
        x86_64)
            echo "/lib64/ld-linux-x86-64.so.2"
            ;;
        aarch64)
            echo "/lib/ld-linux-aarch64.so.1"
            ;;
        *)
            echo ""
            ;;
    esac
}
