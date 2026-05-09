#!/usr/bin/env bash
set -euo pipefail

APP_NAME="crossmacro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_SMOKE="$SCRIPT_DIR/cli-smoke.sh"
DEFAULT_IMAGE="${DEB_SMOKE_IMAGE:-ubuntu:24.04}"
CONTAINER_ENGINE="${CONTAINER_ENGINE:-}"

usage() {
  cat <<'USAGE'
Usage: deb-package.sh <package.deb> [--image <container-image>] [--no-container]

Validates a CrossMacro Debian package without installing it on the host:
  - verifies the .deb exists
  - inspects package metadata and payload with dpkg-deb
  - checks expected installed paths, dependencies, service, policy, udev, manpage, and CLI symlink
  - installs inside a container when podman/docker is available
  - runs shared CLI smoke against /usr/bin/crossmacro inside that container where practical

Options:
  --image <container-image>  Container image for install smoke (default: ubuntu:24.04)
  --no-container            Skip container install smoke after static package checks
  -h, --help                Show this help
USAGE
}

fail() {
  echo "DEB smoke failed: $1" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

find_container_engine() {
  if [ -n "$CONTAINER_ENGINE" ]; then
    command -v "$CONTAINER_ENGINE" >/dev/null 2>&1 || fail "CONTAINER_ENGINE not found: $CONTAINER_ENGINE"
    echo "$CONTAINER_ENGINE"
    return 0
  fi

  if command -v podman >/dev/null 2>&1; then
    echo podman
    return 0
  fi

  if command -v docker >/dev/null 2>&1; then
    echo docker
    return 0
  fi

  return 1
}

assert_contains() {
  local name="$1"
  local haystack="$2"
  local needle="$3"
  printf '%s\n' "$haystack" | grep -F "$needle" >/dev/null || fail "$name missing: $needle"
}

assert_payload_regex() {
  local name="$1"
  local payload="$2"
  local regex="$3"
  printf '%s\n' "$payload" | grep -E "$regex" >/dev/null || fail "$name missing from package payload"
}

run_container_smoke() {
  local package="$1"
  local image="$2"
  local engine="$3"
  local package_name
  package_name="$(basename "$package")"

  "$engine" run --rm \
    -v "$(cd "$(dirname "$package")" && pwd):/artifacts:ro" \
    -v "$SCRIPT_DIR:/smoke:ro" \
    "$image" \
    sh -euxc '
      export DEBIAN_FRONTEND=noninteractive
      apt-get update
      apt-get install -y --no-install-recommends ca-certificates dpkg apt-utils
      apt-get install -y --no-install-recommends "/artifacts/$1" || apt-get -f install -y --no-install-recommends
      test -x /usr/bin/crossmacro
      /smoke/cli-smoke.sh --binary /usr/bin/crossmacro
    ' deb-container-smoke "$package_name"
}

package=""
image="$DEFAULT_IMAGE"
skip_container=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --image)
      [ "$#" -ge 2 ] || fail "--image requires a value"
      image="$2"
      shift 2
      ;;
    --no-container)
      skip_container=1
      shift
      ;;
    --*)
      fail "unknown option: $1"
      ;;
    *)
      [ -z "$package" ] || fail "only one .deb path may be provided"
      package="$1"
      shift
      ;;
  esac
done

[ -n "$package" ] || fail "missing .deb artifact path"
[ -f "$package" ] || fail "missing .deb artifact: $package"

require_command dpkg-deb
[ -x "$CLI_SMOKE" ] || fail "shared CLI smoke helper not executable: $CLI_SMOKE"

metadata="$(dpkg-deb -f "$package")"
payload="$(dpkg-deb -c "$package")"

assert_contains "Package metadata" "$metadata" "Package: $APP_NAME"
assert_contains "Package metadata" "$metadata" "Architecture:"
assert_contains "Package metadata" "$metadata" "Version:"
assert_contains "Package dependency" "$metadata" "libicu"
assert_contains "Package dependency" "$metadata" "libxtst6"
assert_contains "Package dependency" "$metadata" "libsystemd0"

assert_payload_regex "UI binary" "$payload" '(^| )\.\/usr\/lib\/crossmacro\/CrossMacro\.UI$'
assert_payload_regex "daemon binary" "$payload" '(^| )\.\/usr\/lib\/crossmacro\/daemon\/CrossMacro\.Daemon$'
assert_payload_regex "CLI symlink" "$payload" '(^| )\.\/usr\/bin\/crossmacro$'
assert_payload_regex "systemd service" "$payload" '(^| )\.\/usr\/lib\/systemd\/system\/crossmacro\.service$'
assert_payload_regex "udev rules" "$payload" '(^| )\.\/usr\/lib\/udev\/rules\.d\/99-crossmacro\.rules$'
assert_payload_regex "polkit policy" "$payload" '(^| )\.\/usr\/share\/polkit-1\/actions\/io\.github\.alper_han\.crossmacro\.policy$'
assert_payload_regex "manpage" "$payload" '(^| )\.\/usr\/share\/man\/man1\/crossmacro\.1\.gz$'

if [ "$skip_container" -eq 0 ]; then
  if engine="$(find_container_engine)"; then
    run_container_smoke "$package" "$image" "$engine"
  else
    echo "DEB smoke: container install smoke skipped; neither podman nor docker is available." >&2
  fi
fi

echo "DEB package smoke: OK"
