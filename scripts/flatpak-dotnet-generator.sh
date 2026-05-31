#!/usr/bin/env bash
# flatpak-dotnet-generator.sh
# Bash alternative to flatpak-dotnet-generator.py
# Generates nuget-sources.json for offline Flatpak builds

set -euo pipefail

# Defaults - bump these to latest versions
FREEDESKTOP_DEFAULT="25.08"
DOTNET_DEFAULT="10"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

usage() {
    cat << EOF
Usage: $(basename "$0") [OPTIONS] OUTPUT PROJECT [PROJECT...]

Generate NuGet sources JSON for Flatpak offline builds.

Arguments:
    OUTPUT      Output JSON file path
    PROJECT     One or more .csproj files to process

Options:
    -r, --runtime RUNTIME     Target runtime (repeatable, comma-separated supported;
                              defaults: linux-x64,linux-arm64)
    -f, --freedesktop VER     Freedesktop SDK version (default: $FREEDESKTOP_DEFAULT)
    -d, --dotnet VER          .NET SDK version (default: $DOTNET_DEFAULT)
    -h, --help                Show this help message

Example:
    $(basename "$0") nuget-sources.json src/MyApp/MyApp.csproj
    $(basename "$0") nuget-sources.json src/MyApp/MyApp.csproj -r linux-x64 -r linux-arm64
EOF
    exit 1
}

# Parse arguments
RUNTIMES=("linux-x64" "linux-arm64")
RUNTIME_OVERRIDDEN=false
FREEDESKTOP="$FREEDESKTOP_DEFAULT"
DOTNET="$DOTNET_DEFAULT"
OUTPUT=""
PROJECTS=()

while [[ $# -gt 0 ]]; do
    case $1 in
        -r|--runtime)
            if [[ "$RUNTIME_OVERRIDDEN" == "false" ]]; then
                RUNTIMES=()
                RUNTIME_OVERRIDDEN=true
            fi

            IFS=',' read -r -a parsed_runtimes <<< "$2"
            for parsed_runtime in "${parsed_runtimes[@]}"; do
                if [[ -n "$parsed_runtime" ]]; then
                    RUNTIMES+=("$parsed_runtime")
                fi
            done
            shift 2
            ;;
        -f|--freedesktop)
            FREEDESKTOP="$2"
            shift 2
            ;;
        -d|--dotnet)
            DOTNET="$2"
            shift 2
            ;;
        -h|--help)
            usage
            ;;
        -*)
            echo -e "${RED}Error: Unknown option $1${NC}" >&2
            usage
            ;;
        *)
            if [[ -z "$OUTPUT" ]]; then
                OUTPUT="$1"
            else
                PROJECTS+=("$1")
            fi
            shift
            ;;
    esac
done

# Validate arguments
if [[ -z "$OUTPUT" ]] || [[ ${#PROJECTS[@]} -eq 0 ]]; then
    echo -e "${RED}Error: OUTPUT and at least one PROJECT are required${NC}" >&2
    usage
fi

if [[ ${#RUNTIMES[@]} -eq 0 ]]; then
    echo -e "${RED}Error: At least one runtime must be provided${NC}" >&2
    usage
fi

if ! command -v jq >/dev/null 2>&1; then
    echo -e "${RED}Error: 'jq' is required${NC}" >&2
    exit 1
fi

echo -e "${GREEN}=== Flatpak .NET NuGet Sources Generator ===${NC}"
echo "Output: $OUTPUT"
echo "Projects: ${PROJECTS[*]}"
echo "Runtimes: ${RUNTIMES[*]}"
echo "Freedesktop: $FREEDESKTOP"
echo ".NET: $DOTNET"
echo

# Create temp directory in current working dir (accessible from flatpak sandbox)
TMPDIR="$(pwd)/.nuget-temp-$$"
mkdir -p "$TMPDIR"
TEMP_ITEMS=$(mktemp)
trap 'rm -rf "$TMPDIR" "$TEMP_ITEMS"' EXIT

echo -e "${YELLOW}Restoring NuGet packages in Flatpak sandbox...${NC}"

# Restore each project
for PROJECT in "${PROJECTS[@]}"; do
    for RUNTIME in "${RUNTIMES[@]}"; do
        echo "  Restoring: $PROJECT (RID: $RUNTIME)"

        flatpak run \
            --env=DOTNET_CLI_TELEMETRY_OPTOUT=true \
            --env=DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
            --command=sh \
            --runtime="org.freedesktop.Sdk//${FREEDESKTOP}" \
            --share=network \
            --filesystem=host \
            "org.freedesktop.Sdk.Extension.dotnet${DOTNET}//${FREEDESKTOP}" \
            -c "PATH=\"\${PATH}:/usr/lib/sdk/dotnet${DOTNET}/bin\" LD_LIBRARY_PATH=\"\$LD_LIBRARY_PATH:/usr/lib/sdk/dotnet${DOTNET}/lib\" dotnet restore --packages \"$TMPDIR\" \"$PROJECT\" -r \"$RUNTIME\" -p:SelfContained=true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial -p:PublishAot=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false --source https://api.nuget.org/v3/index.json --source /usr/lib/sdk/dotnet${DOTNET}/nuget/packages" \
            2>&1
    done
done

echo -e "${YELLOW}Generating JSON from downloaded packages...${NC}"

# Use find instead of globbing for better compatibility.
while IFS= read -r -d '' sha_file; do
    # Extract package info from path structure: $TMPDIR/packagename/version/packagename.version.nupkg.sha512
    version_dir=$(dirname "$sha_file")
    package_dir=$(dirname "$version_dir")

    name=$(basename "$package_dir")
    version=$(basename "$version_dir")
    filename="${name}.${version}.nupkg"

    # Decode base64 SHA512 to hex.
    sha512=$(base64 -d < "$sha_file" | xxd -p | tr -d '\n')

    jq -n \
        --arg url "https://api.nuget.org/v3-flatcontainer/${name}/${version}/${filename}" \
        --arg sha512 "$sha512" \
        --arg filename "$filename" \
        '{type: "file", url: $url, sha512: $sha512, dest: "nuget-sources", "dest-filename": $filename}' \
        >> "$TEMP_ITEMS"
done < <(find "$TMPDIR" -name "*.nupkg.sha512" -print0 | sort -z)

jq -s . "$TEMP_ITEMS" > "$OUTPUT"

# Count packages
PACKAGE_COUNT=$(grep -c '"type": "file"' "$OUTPUT" || echo "0")

echo -e "${GREEN}✓ Generated $OUTPUT with $PACKAGE_COUNT packages${NC}"
