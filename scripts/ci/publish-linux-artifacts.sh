#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

usage() {
    cat <<'USAGE'
Usage: publish-linux-artifacts.sh --rid <rid> --arch <arch> --version <version> --ui-output <dir> --daemon-output <dir> [options]

Publishes CrossMacro Linux UI and daemon artifacts with the CI/release workflow options,
then smokes the published UI binary.

Required:
  --rid <rid>               .NET runtime identifier, for example linux-x64 or linux-arm64.
  --arch <arch>             Target architecture label, for example x86_64 or aarch64.
  --version <version>       Product version passed to dotnet publish.
  --ui-output <dir>         Output directory for the UI publish.
  --daemon-output <dir>     Output directory for the daemon publish.

Options:
  --daemon-publish-args <args>
                            Extra daemon publish arguments used by the workflow matrix.
  --smoke-helper <path>     Smoke helper path. Defaults to scripts/smoke/cli-smoke.sh,
                            then falls back to scripts/smoke-published-linux-artifacts.sh.
  --ui-project <path>       Override UI project path.
  --daemon-project <path>   Override daemon project path.
  --skip-smoke              Publish without running the smoke helper.
  -h, --help                Show this help.
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

RID=""
TARGET_ARCH=""
VERSION_VALUE=""
UI_OUTPUT=""
DAEMON_OUTPUT=""
DAEMON_PUBLISH_ARGS=""
SMOKE_HELPER=""
UI_PROJECT="src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj"
DAEMON_PROJECT="src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"
SKIP_SMOKE="false"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --rid)
            require_value "$@"
            RID="$2"
            shift 2
            ;;
        --arch)
            require_value "$@"
            TARGET_ARCH="$2"
            shift 2
            ;;
        --version)
            require_value "$@"
            VERSION_VALUE="$2"
            shift 2
            ;;
        --ui-output)
            require_value "$@"
            UI_OUTPUT="$2"
            shift 2
            ;;
        --daemon-output)
            require_value "$@"
            DAEMON_OUTPUT="$2"
            shift 2
            ;;
        --daemon-publish-args)
            require_value "$@"
            DAEMON_PUBLISH_ARGS="$2"
            shift 2
            ;;
        --smoke-helper)
            require_value "$@"
            SMOKE_HELPER="$2"
            shift 2
            ;;
        --ui-project)
            require_value "$@"
            UI_PROJECT="$2"
            shift 2
            ;;
        --daemon-project)
            require_value "$@"
            DAEMON_PROJECT="$2"
            shift 2
            ;;
        --skip-smoke)
            SKIP_SMOKE="true"
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

[ -n "$RID" ] || fail "--rid is required"
[ -n "$TARGET_ARCH" ] || fail "--arch is required"
[ -n "$VERSION_VALUE" ] || fail "--version is required"
[ -n "$UI_OUTPUT" ] || fail "--ui-output is required"
[ -n "$DAEMON_OUTPUT" ] || fail "--daemon-output is required"

cd "$PROJECT_ROOT"

case "$RID" in
    linux-x64|linux-arm64)
        ;;
    *)
        fail "unsupported Linux RID '$RID'"
        ;;
esac

case "$TARGET_ARCH" in
    x86_64|aarch64)
        ;;
    *)
        fail "unsupported Linux arch '$TARGET_ARCH'"
        ;;
esac

if [ -z "$SMOKE_HELPER" ]; then
    if [ -x "$PROJECT_ROOT/scripts/smoke/cli-smoke.sh" ] || [ -f "$PROJECT_ROOT/scripts/smoke/cli-smoke.sh" ]; then
        SMOKE_HELPER="$PROJECT_ROOT/scripts/smoke/cli-smoke.sh"
    else
        SMOKE_HELPER="$PROJECT_ROOT/scripts/smoke-published-linux-artifacts.sh"
    fi
elif [[ "$SMOKE_HELPER" != /* ]]; then
    SMOKE_HELPER="$PROJECT_ROOT/$SMOKE_HELPER"
fi

UI_BINARY="$UI_OUTPUT/CrossMacro.UI"

mkdir -p "$UI_OUTPUT" "$DAEMON_OUTPUT"

dotnet publish "$UI_PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:PublishAot=false \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:Version="$VERSION_VALUE" \
    -o "$UI_OUTPUT/"

if [ -n "$DAEMON_PUBLISH_ARGS" ]; then
    read -r -a DAEMON_ARGS <<< "$DAEMON_PUBLISH_ARGS"
else
    DAEMON_ARGS=()
fi

dotnet publish "$DAEMON_PROJECT" \
    -c Release \
    -r "$RID" \
    "${DAEMON_ARGS[@]}" \
    -p:Version="$VERSION_VALUE" \
    -o "$DAEMON_OUTPUT/"

if [ "$SKIP_SMOKE" = "true" ]; then
    echo "Skipping published Linux artifact smoke for $RID ($TARGET_ARCH)."
    exit 0
fi

chmod +x "$SMOKE_HELPER"
if [ "$(basename "$SMOKE_HELPER")" = "cli-smoke.sh" ]; then
    "$SMOKE_HELPER" --binary "$UI_BINARY"
else
    "$SMOKE_HELPER" --ui-binary "$UI_BINARY"
fi
