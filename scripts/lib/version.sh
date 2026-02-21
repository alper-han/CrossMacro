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

get_msix_version() {
    local version
    version="$(get_version)"
    echo "${version}.0"
}
