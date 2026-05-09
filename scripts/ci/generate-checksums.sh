#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_MANIFEST="$SCRIPT_DIR/expected-release-assets.json"

usage() {
    cat <<'EOF'
Usage: generate-checksums.sh --directory <dir> [--manifest <path>] [--version <version>] [--attach-flatpak true|false] [--attach-msix true|false]

Generate SHA256SUMS for the enabled release assets in a directory and verify it with sha256sum -c.

Options:
  --directory <dir>      Release artifact directory to validate and checksum.
  --manifest <path>      Asset manifest JSON. Defaults to scripts/ci/expected-release-assets.json.
  --version              Override the manifest sample version when resolving artifact names.
  --attach-flatpak       Include Flatpak assets. Defaults to true.
  --attach-msix          Include MSIX assets. Defaults to true.
  -h, --help             Show this help text.
EOF
}

parse_bool() {
    case "${1,,}" in
        true|1|yes|y|on)
            printf 'true'
            ;;
        false|0|no|n|off)
            printf 'false'
            ;;
        *)
            echo "Error: expected true or false, got '$1'" >&2
            exit 1
            ;;
    esac
}

manifest_path="$DEFAULT_MANIFEST"
directory=""
version=""
attach_flatpak="true"
attach_msix="true"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --directory)
            [ "$#" -ge 2 ] || { echo "Error: --directory requires a value" >&2; exit 1; }
            directory="$2"
            shift 2
            ;;
        --manifest)
            [ "$#" -ge 2 ] || { echo "Error: --manifest requires a value" >&2; exit 1; }
            manifest_path="$2"
            shift 2
            ;;
        --version)
            [ "$#" -ge 2 ] || { echo "Error: --version requires a value" >&2; exit 1; }
            version="$2"
            shift 2
            ;;
        --attach-flatpak)
            [ "$#" -ge 2 ] || { echo "Error: --attach-flatpak requires a value" >&2; exit 1; }
            attach_flatpak="$(parse_bool "$2")"
            shift 2
            ;;
        --attach-msix)
            [ "$#" -ge 2 ] || { echo "Error: --attach-msix requires a value" >&2; exit 1; }
            attach_msix="$(parse_bool "$2")"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Error: unknown option '$1'" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if [ -z "$directory" ]; then
    echo "Error: --directory is required" >&2
    usage >&2
    exit 1
fi

if [ ! -f "$manifest_path" ]; then
    echo "Error: manifest not found: $manifest_path" >&2
    exit 1
fi

if [ ! -d "$directory" ]; then
    echo "Error: artifact directory not found: $directory" >&2
    exit 1
fi

mapfile -t expected_files < <(
    python3 - "$SCRIPT_DIR" "$manifest_path" "$attach_flatpak" "$attach_msix" "$version" <<'PY'
import pathlib
import sys

script_dir = pathlib.Path(sys.argv[1])
manifest_path = pathlib.Path(sys.argv[2])
attach_flatpak = sys.argv[3].lower() == 'true'
attach_msix = sys.argv[4].lower() == 'true'
version = sys.argv[5] or None
sys.path.insert(0, str(script_dir))
import verify_artifacts

manifest = verify_artifacts.rewrite_manifest_for_version(verify_artifacts.load_manifest(manifest_path), version)

files = []
for asset in verify_artifacts.expected_assets(manifest, attach_flatpak, attach_msix):
    file_name = asset.get('file')
    if isinstance(file_name, str) and file_name and file_name != 'SHA256SUMS':
        files.append(file_name)

for file_name in files:
    print(file_name)
PY
)

if [ "${#expected_files[@]}" -eq 0 ]; then
    echo "Error: manifest produced no checksum inputs" >&2
    exit 1
fi

missing=0
for file_name in "${expected_files[@]}"; do
    if [ ! -f "$directory/$file_name" ]; then
        echo "Error: missing artifact: $file_name" >&2
        missing=1
    fi
done

if [ "$missing" -ne 0 ]; then
    exit 1
fi

checksum_file="$directory/SHA256SUMS"
tmp_file="$directory/SHA256SUMS.tmp"
trap 'rm -f "$tmp_file"' EXIT

(
    cd "$directory"
    sha256sum "${expected_files[@]}" > SHA256SUMS.tmp
    mv SHA256SUMS.tmp SHA256SUMS
    sha256sum -c SHA256SUMS
)
