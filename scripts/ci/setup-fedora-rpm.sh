#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ "$(id -u)" -ne 0 ] || { [ ! -f /.dockerenv ] && [ ! -f /run/.containerenv ]; }; then
    echo "Fedora CI setup requires root inside a disposable container." >&2
    exit 1
fi
mapfile -t packages < <(sed '/^[[:space:]]*#/d; /^[[:space:]]*$/d' "$script_dir/../packaging/rpm/ci-packages.txt")
started=$SECONDS
dnf install -y --setopt=keepcache=True --setopt=install_weak_deps=False "${packages[@]}"
command -v rpmbuild checkmodule semodule_package patchelf file rpm
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
        echo "Fedora dependency setup: $((SECONDS - started)) seconds"
        echo "RPM environment: $(rpm --version); architecture: $(uname -m)"
    } >> "$GITHUB_STEP_SUMMARY"
fi
