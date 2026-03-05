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

map_elf_machine_to_arch() {
    local machine="${1:-}"
    case "$machine" in
        "Advanced Micro Devices X86-64"|x86-64|X86-64|x86_64)
            echo "x86_64"
            ;;
        AArch64|aarch64)
            echo "aarch64"
            ;;
        *)
            echo "Error: Unsupported ELF machine '$machine'." >&2
            return 1
            ;;
    esac
}

detect_binary_arch() {
    local binary_path="${1:-}"
    if [ -z "$binary_path" ] || [ ! -f "$binary_path" ]; then
        echo "Error: Binary not found: '$binary_path'." >&2
        return 1
    fi

    local machine=""
    if command -v readelf >/dev/null; then
        machine="$(LC_ALL=C readelf -h "$binary_path" 2>/dev/null | awk -F: '/Machine:/ {gsub(/^[ \t]+/, "", $2); print $2; exit}')"
    fi

    if [ -n "$machine" ]; then
        map_elf_machine_to_arch "$machine"
        return $?
    fi

    if command -v file >/dev/null; then
        local file_output
        file_output="$(LC_ALL=C file -Lb "$binary_path" 2>/dev/null || true)"
        case "$file_output" in
            *x86-64*)
                echo "x86_64"
                return 0
                ;;
            *AArch64*|*aarch64*)
                echo "aarch64"
                return 0
                ;;
        esac
    fi

    echo "Error: Could not detect architecture for '$binary_path'." >&2
    return 1
}

verify_binary_arch() {
    local binary_path="${1:-}"
    local expected_arch
    expected_arch="$(normalize_arch "${2:-$(get_target_arch)}")"

    local detected_arch
    detected_arch="$(detect_binary_arch "$binary_path")" || return 1
    detected_arch="$(normalize_arch "$detected_arch")"

    if [ "$detected_arch" != "$expected_arch" ]; then
        echo "Error: Binary architecture mismatch for '$binary_path': detected '$detected_arch', expected '$expected_arch'." >&2
        return 1
    fi
}
