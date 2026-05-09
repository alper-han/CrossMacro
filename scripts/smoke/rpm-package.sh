#!/usr/bin/env bash
set -euo pipefail

APP_NAME="crossmacro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_SMOKE="$SCRIPT_DIR/cli-smoke.sh"
DEFAULT_IMAGE="${RPM_SMOKE_IMAGE:-fedora:41}"
CONTAINER_ENGINE="${CONTAINER_ENGINE:-}"

usage() {
  cat <<'USAGE'
Usage: rpm-package.sh <package.rpm> [--image <container-image>] [--no-container]

Validates a CrossMacro RPM package without installing it on the host:
  - verifies the .rpm exists
  - inspects dependencies with rpm -qpR and payload with rpm -qpl
  - checks expected installed paths, dependencies, service, policy, udev, SELinux policy, manpage, and CLI symlink
  - installs inside a Fedora container when podman/docker is available
  - runs shared CLI smoke against /usr/bin/crossmacro inside that container where practical

Options:
  --image <container-image>  Container image for install smoke (default: fedora:41)
  --no-container            Skip container install smoke after static package checks
  -h, --help                Show this help
USAGE
}

fail() {
  echo "RPM smoke failed: $1" >&2
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

assert_payload_path() {
  local payload="$1"
  local path="$2"
  printf '%s\n' "$payload" | grep -Fx "$path" >/dev/null || fail "payload missing: $path"
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
      dnf install -y "/artifacts/$1"
      test -x /usr/bin/crossmacro
      /smoke/cli-smoke.sh --binary /usr/bin/crossmacro
    ' rpm-container-smoke "$package_name"
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
      [ -z "$package" ] || fail "only one .rpm path may be provided"
      package="$1"
      shift
      ;;
  esac
done

[ -n "$package" ] || fail "missing .rpm artifact path"
[ -f "$package" ] || fail "missing .rpm artifact: $package"

require_command rpm
[ -x "$CLI_SMOKE" ] || fail "shared CLI smoke helper not executable: $CLI_SMOKE"

name="$(rpm -qp --queryformat '%{NAME}' "$package")"
arch="$(rpm -qp --queryformat '%{ARCH}' "$package")"
version="$(rpm -qp --queryformat '%{VERSION}-%{RELEASE}' "$package")"
requires="$(rpm -qpR "$package")"
payload="$(rpm -qpl "$package")"

[ "$name" = "$APP_NAME" ] || fail "unexpected package name: $name"
[ -n "$arch" ] || fail "missing package architecture"
[ -n "$version" ] || fail "missing package version"
assert_contains "RPM dependency" "$requires" "libicu"
assert_contains "RPM dependency" "$requires" "libXtst"
assert_contains "RPM dependency" "$requires" "systemd-libs"

assert_payload_path "$payload" "/usr/lib/crossmacro"
assert_payload_path "$payload" "/usr/bin/crossmacro"
assert_payload_path "$payload" "/usr/lib/systemd/system/crossmacro.service"
assert_payload_path "$payload" "/usr/lib/udev/rules.d/99-crossmacro.rules"
assert_payload_path "$payload" "/usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy"
assert_payload_path "$payload" "/usr/share/selinux/packages/crossmacro/crossmacro.pp"
printf '%s\n' "$payload" | grep -E '^/usr/share/man/man1/crossmacro\.1(\.gz)?$' >/dev/null || fail "payload missing manpage"

if [ "$skip_container" -eq 0 ]; then
  if engine="$(find_container_engine)"; then
    run_container_smoke "$package" "$image" "$engine"
  else
    echo "RPM smoke: container install smoke skipped; neither podman nor docker is available." >&2
  fi
fi

echo "RPM package smoke: OK"
