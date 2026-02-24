#!/usr/bin/env bash

set -euo pipefail

get_repo_root() {
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    cd "$script_dir/../.." && pwd
}

validate_version() {
    local version="$1"
    [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
}

validate_release_tag() {
    local tag="$1"
    local canonical
    local base

    canonical="${tag#v}"
    if [ "$canonical" = "$tag" ]; then
        return 1
    fi

    if ! [[ "$canonical" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.+-]+)?$ ]]; then
        return 1
    fi

    base="${canonical%%[-+]*}"
    validate_version "$base"
}

read_version_file() {
    local repo_root="$1"
    local version_file="$repo_root/VERSION"

    if [ ! -f "$version_file" ]; then
        echo "Error: VERSION file not found at $version_file" >&2
        return 1
    fi

    tr -d '[:space:]' < "$version_file"
}

get_version() {
    local repo_root version
    repo_root="$(get_repo_root)"

    if [ -n "${VERSION:-}" ]; then
        version="$VERSION"
    else
        version="$(read_version_file "$repo_root")"
    fi

    if ! validate_version "$version"; then
        echo "Error: Invalid version format '$version' (expected X.Y.Z)" >&2
        return 1
    fi

    echo "$version"
}

normalize_release_tag() {
    local tag="$1"
    if [ -z "$tag" ]; then
        return 1
    fi

    if [[ "$tag" != v* ]]; then
        tag="v${tag}"
    fi

    echo "$tag"
}

get_release_tag() {
    local tag

    if [ -n "${SOURCE_TAG:-}" ]; then
        tag="$SOURCE_TAG"
    else
        tag="v$(get_version)"
    fi

    tag="$(normalize_release_tag "$tag")"
    if ! validate_release_tag "$tag"; then
        echo "Error: Invalid SOURCE_TAG '$tag' (expected vX.Y.Z or vX.Y.Z-suffix)" >&2
        return 1
    fi

    echo "$tag"
}

get_canonical_package_version() {
    local canonical tag

    if [ -n "${PACKAGE_VERSION_CANONICAL:-}" ]; then
        canonical="$PACKAGE_VERSION_CANONICAL"
    else
        tag="$(get_release_tag)"
        canonical="${tag#v}"
    fi

    if ! [[ "$canonical" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.+-]+)?$ ]]; then
        echo "Error: Invalid package version '$canonical' (expected X.Y.Z or X.Y.Z-suffix)" >&2
        return 1
    fi

    echo "$canonical"
}

parse_semver_parts() {
    local canonical="$1"
    local base prerelease

    base="${canonical%%[-+]*}"
    if ! validate_version "$base"; then
        echo "Error: Invalid semantic base version '$base' from '$canonical'" >&2
        return 1
    fi

    prerelease=""
    if [[ "$canonical" == *-* ]]; then
        prerelease="${canonical#*-}"
        prerelease="${prerelease%%+*}"
    fi

    echo "${base}|${prerelease}"
}

get_base_version() {
    local canonical parsed
    canonical="${1:-$(get_canonical_package_version)}"
    parsed="$(parse_semver_parts "$canonical")"
    echo "${parsed%%|*}"
}

get_prerelease_label() {
    local canonical parsed
    canonical="${1:-$(get_canonical_package_version)}"
    parsed="$(parse_semver_parts "$canonical")"
    echo "${parsed#*|}"
}

normalize_token() {
    local token="$1"
    local kind="$2"

    case "$kind" in
        deb)
            printf '%s' "$token" | sed -E 's/[^0-9A-Za-z.+~-]/./g; s/\.+/./g; s/^\.//; s/\.$//'
            ;;
        rpm|aur)
            printf '%s' "$token" | sed -E 's/[-+]/./g; s/[^0-9A-Za-z._]/./g; s/\.+/./g; s/^\.//; s/\.$//'
            ;;
        filename)
            printf '%s' "$token" | sed -E 's/[^0-9A-Za-z._+-]/./g; s/\.+/./g; s/^\.//; s/\.$//'
            ;;
        *)
            printf '%s' "$token"
            ;;
    esac
}

to_deb_version() {
    local canonical base prerelease parsed normalized_pre
    canonical="${1:-$(get_canonical_package_version)}"
    parsed="$(parse_semver_parts "$canonical")"
    base="${parsed%%|*}"
    prerelease="${parsed#*|}"

    if [ -z "$prerelease" ]; then
        echo "$base"
        return 0
    fi

    normalized_pre="$(normalize_token "$prerelease" deb)"
    if [ -z "$normalized_pre" ]; then
        normalized_pre="pre"
    fi
    echo "${base}~${normalized_pre}"
}

to_rpm_version() {
    local canonical
    canonical="${1:-$(get_canonical_package_version)}"
    get_base_version "$canonical"
}

to_rpm_release() {
    local canonical prerelease release_base normalized_pre
    canonical="${1:-$(get_canonical_package_version)}"
    prerelease="$(get_prerelease_label "$canonical")"
    release_base="${RPM_RELEASE_BASE:-1}"

    if ! [[ "$release_base" =~ ^[0-9]+$ ]]; then
        echo "Error: Invalid RPM_RELEASE_BASE '$release_base' (expected integer)" >&2
        return 1
    fi

    if [ -z "$prerelease" ]; then
        echo "$release_base"
        return 0
    fi

    normalized_pre="$(normalize_token "$prerelease" rpm)"
    if [ -z "$normalized_pre" ]; then
        normalized_pre="pre"
    fi

    # Lower release number keeps prerelease sorted before stable builds.
    echo "0.${release_base}.${normalized_pre}"
}

to_aur_pkgver() {
    local canonical normalized
    canonical="${1:-$(get_canonical_package_version)}"
    normalized="$(normalize_token "$canonical" aur)"
    if [ -z "$normalized" ]; then
        echo "Error: Failed to normalize AUR pkgver from '$canonical'" >&2
        return 1
    fi
    echo "$normalized"
}

to_filename_version() {
    local canonical normalized
    canonical="${1:-$(get_canonical_package_version)}"
    normalized="$(normalize_token "$canonical" filename)"
    if [ -z "$normalized" ]; then
        echo "Error: Failed to normalize filename version from '$canonical'" >&2
        return 1
    fi
    echo "$normalized"
}

get_msix_version() {
    local version
    version="$(get_version)"
    echo "${version}.0"
}
