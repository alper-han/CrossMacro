#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPT_DIR/lib/version.sh"

MODE="${1:-}"
if [ "$MODE" != "--check" ] && [ "$MODE" != "--write" ]; then
    echo "Usage: $0 [--check|--write]" >&2
    exit 1
fi

REPO_ROOT="$(get_repo_root)"
VERSION_VALUE="$(get_version)"
MSIX_VERSION_VALUE="$(get_msix_version)"

PKGBUILD_FILE="$REPO_ROOT/scripts/packaging/arch/PKGBUILD"
MSIX_MANIFEST_FILE="$REPO_ROOT/scripts/msix/AppxManifest.xml"
FLATHUB_FILE="$REPO_ROOT/flatpak/io.github.alper_han.crossmacro.flathub.yml"

patch_pkgbuild() {
    local tmp
    tmp="$(mktemp)"
    sed -E "s/^pkgver=.*/pkgver=${VERSION_VALUE}/" "$PKGBUILD_FILE" > "$tmp"
    mv "$tmp" "$PKGBUILD_FILE"
}

patch_msix_manifest() {
    perl -0pi -e "s/Version=\"[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+\"/Version=\"${MSIX_VERSION_VALUE}\"/" "$MSIX_MANIFEST_FILE"
}

patch_flathub_tag() {
    local tmp
    tmp="$(mktemp)"
    sed -E "s/^([[:space:]]*tag:[[:space:]]*)v[0-9]+\\.[0-9]+\\.[0-9]+/\\1v${VERSION_VALUE}/" "$FLATHUB_FILE" > "$tmp"
    mv "$tmp" "$FLATHUB_FILE"
}

check_value() {
    local name="$1"
    local actual="$2"
    local expected="$3"
    if [ "$actual" != "$expected" ]; then
        echo "Drift detected: $name (expected '$expected', got '$actual')" >&2
        return 1
    fi
    echo "OK: $name = $actual"
}

if [ "$MODE" = "--write" ]; then
    patch_pkgbuild
    patch_msix_manifest
    patch_flathub_tag
fi

PKGBUILD_VERSION="$(sed -nE 's/^pkgver=([0-9]+\.[0-9]+\.[0-9]+)$/\1/p' "$PKGBUILD_FILE" | head -n1)"
MSIX_MANIFEST_VERSION="$(sed -nE 's/^[[:space:]]*Version=\"([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)\"/\1/p' "$MSIX_MANIFEST_FILE" | head -n1)"
FLATHUB_TAG_VERSION="$(sed -nE 's/^[[:space:]]*tag:[[:space:]]*v([0-9]+\.[0-9]+\.[0-9]+)$/\1/p' "$FLATHUB_FILE" | head -n1)"

check_value "PKGBUILD pkgver" "$PKGBUILD_VERSION" "$VERSION_VALUE"
check_value "MSIX Identity Version" "$MSIX_MANIFEST_VERSION" "$MSIX_VERSION_VALUE"
check_value "Flathub tag" "$FLATHUB_TAG_VERSION" "$VERSION_VALUE"

echo "Version sync validation completed for $VERSION_VALUE"
