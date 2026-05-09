#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
# shellcheck source=../lib/version.sh
source "$PROJECT_ROOT/scripts/lib/version.sh"

usage() {
    cat <<'USAGE'
Usage: resolve-release-metadata.sh --mode <ci|release> [options]

Resolves CrossMacro release metadata from VERSION and an optional source tag.

Required:
  --mode <ci|release>       ci for PR/package checks, release for release validation.

Options:
  --source-tag <tag>        Source tag to normalize or validate. Defaults to v<VERSION>.
  --input-version <version> Optional workflow_dispatch version; must match VERSION.
  --publish-prerelease <true|false>
                            Resolve a prerelease source tag for release mode.
  --prerelease-tag <tag>    Manual prerelease tag, normalized to v<tag> when needed.
  --run-number <number>     Run number for default prerelease tag. Defaults to GITHUB_RUN_NUMBER.
  --enforce-version-file-match
                            Require the resolved source tag base to match VERSION.
  --print                   Print key=value metadata to stdout.
  -h, --help                Show this help.

Outputs:
  version
  source_tag
  package_version_canonical
  prerelease_label
  is_prerelease_release
USAGE
}

fail() {
    echo "Error: $*" >&2
    exit 1
}

require_value() {
    if [ "$#" -lt 2 ] || [ -z "${2:-}" ]; then
        fail "missing value for $1"
    fi
}

write_output() {
    local key="$1"
    local value="$2"

    if [ "${PRINT_OUTPUT:-false}" = "true" ]; then
        printf '%s=%s
' "$key" "$value"
    fi

    if [ -n "${GITHUB_OUTPUT:-}" ]; then
        printf '%s=%s
' "$key" "$value" >> "$GITHUB_OUTPUT"
    fi
}

MODE=""
SOURCE_TAG_ARG=""
INPUT_VERSION=""
PUBLISH_PRERELEASE="false"
PRERELEASE_TAG=""
RUN_NUMBER="${GITHUB_RUN_NUMBER:-}"
PRINT_OUTPUT="false"
ENFORCE_VERSION_FILE_MATCH="false"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --mode)
            require_value "$@"
            MODE="$2"
            shift 2
            ;;
        --source-tag)
            require_value "$@"
            SOURCE_TAG_ARG="$2"
            shift 2
            ;;
        --input-version)
            require_value "$@"
            INPUT_VERSION="$2"
            shift 2
            ;;
        --publish-prerelease)
            require_value "$@"
            PUBLISH_PRERELEASE="$2"
            shift 2
            ;;
        --prerelease-tag)
            require_value "$@"
            PRERELEASE_TAG="$2"
            shift 2
            ;;
        --run-number)
            require_value "$@"
            RUN_NUMBER="$2"
            shift 2
            ;;
        --enforce-version-file-match)
            ENFORCE_VERSION_FILE_MATCH="true"
            shift
            ;;
        --print)
            PRINT_OUTPUT="true"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

case "$MODE" in
    ci|release)
        ;;
    "")
        usage >&2
        fail "--mode is required"
        ;;
    *)
        fail "invalid --mode '$MODE' (expected ci or release)"
        ;;
esac

case "$PUBLISH_PRERELEASE" in
    true|false)
        ;;
    *)
        fail "invalid --publish-prerelease '$PUBLISH_PRERELEASE' (expected true or false)"
        ;;
esac

VERSION_VALUE="$(read_version_file "$PROJECT_ROOT")"
if ! validate_version "$VERSION_VALUE"; then
    fail "Invalid VERSION file value: '$VERSION_VALUE'"
fi

if [ -n "$INPUT_VERSION" ] && [ "$INPUT_VERSION" != "$VERSION_VALUE" ]; then
    fail "workflow_dispatch version '$INPUT_VERSION' does not match VERSION file '$VERSION_VALUE'"
fi

ALLOW_VERSION_DRIFT="false"
SOURCE_TAG=""
IS_PRERELEASE_RELEASE="false"

if [ -n "$SOURCE_TAG_ARG" ]; then
    SOURCE_TAG="$SOURCE_TAG_ARG"
elif [ "$MODE" = "release" ] && [ "$PUBLISH_PRERELEASE" = "true" ]; then
    IS_PRERELEASE_RELEASE="true"
    if [ -n "$PRERELEASE_TAG" ]; then
        SOURCE_TAG="$PRERELEASE_TAG"
        ALLOW_VERSION_DRIFT="true"
    else
        if [ -z "$RUN_NUMBER" ]; then
            fail "--run-number or GITHUB_RUN_NUMBER is required for default prerelease tags"
        fi
        SOURCE_TAG="v${VERSION_VALUE}-pre.${RUN_NUMBER}"
    fi
else
    SOURCE_TAG="v${VERSION_VALUE}"
fi

if [ "$MODE" = "ci" ]; then
    SOURCE_TAG="$(normalize_release_tag "$SOURCE_TAG")" || fail "invalid source tag '$SOURCE_TAG'"
else
    if [[ "$SOURCE_TAG" != v* ]]; then
        fail "invalid source tag '$SOURCE_TAG' (expected vX.Y.Z or vX.Y.Z-suffix)"
    fi
fi

if ! validate_release_tag "$SOURCE_TAG"; then
    fail "invalid source tag '$SOURCE_TAG' (expected vX.Y.Z or vX.Y.Z-suffix)"
fi

PACKAGE_VERSION_CANONICAL="${SOURCE_TAG#v}"
BASE_VERSION="$(get_base_version "$PACKAGE_VERSION_CANONICAL")"
if [ "$ENFORCE_VERSION_FILE_MATCH" = "true" ] && [ "$BASE_VERSION" != "$VERSION_VALUE" ]; then
    if [ "$ALLOW_VERSION_DRIFT" = "true" ]; then
        echo "Warning: prerelease tag base '$BASE_VERSION' differs from VERSION '$VERSION_VALUE' (allowed for manual prerelease)." >&2
    else
        fail "resolved source tag '$SOURCE_TAG' does not match VERSION '$VERSION_VALUE'"
    fi
fi

PRERELEASE_LABEL="$(get_prerelease_label "$PACKAGE_VERSION_CANONICAL")"
if [ -n "$SOURCE_TAG_ARG" ] && [ "$ENFORCE_VERSION_FILE_MATCH" != "true" ]; then
    VERSION_VALUE="$BASE_VERSION"
fi
if [ -n "$PRERELEASE_LABEL" ]; then
    IS_PRERELEASE_RELEASE="true"
fi

write_output version "$VERSION_VALUE"
write_output source_tag "$SOURCE_TAG"
write_output package_version_canonical "$PACKAGE_VERSION_CANONICAL"
write_output prerelease_label "$PRERELEASE_LABEL"
write_output is_prerelease_release "$IS_PRERELEASE_RELEASE"

if [ "$PRINT_OUTPUT" != "true" ] && [ -z "${GITHUB_OUTPUT:-}" ]; then
    echo "Resolved release metadata: version=$VERSION_VALUE source_tag=$SOURCE_TAG prerelease=$IS_PRERELEASE_RELEASE"
fi
