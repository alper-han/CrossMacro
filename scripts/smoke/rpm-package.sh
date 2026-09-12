#!/usr/bin/env bash
set -euo pipefail

APP_NAME="crossmacro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_SMOKE="$SCRIPT_DIR/cli-smoke.sh"
DEFAULT_IMAGE="${RPM_SMOKE_IMAGE:-fedora:44}"
CONTAINER_ENGINE="${CONTAINER_ENGINE:-}"

usage() {
  cat <<'USAGE'
Usage: rpm-package.sh <package.rpm> [--image <container-image>] [--no-container | --install-in-container]

Validates a CrossMacro RPM package without installing it on the host:
  - verifies the .rpm exists
  - inspects dependencies with rpm -qpR and payload with rpm -qpl
  - checks expected installed paths, dependencies, service, policy, udev, SELinux policy, manpage, and CLI symlink
  - installs inside a Fedora container when podman/docker is available
  - runs shared CLI smoke against /usr/bin/crossmacro inside that container where practical

Options:
  --image <container-image>  Container image for install smoke (default: fedora:44)
  --no-container            Skip container install smoke after static package checks
  --install-in-container    Offline install in the current disposable root container (CI)
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

  # The positional parameter is expanded by the container's shell, not this shell.
  # shellcheck disable=SC2016
  "$engine" run --rm \
    -v "$(cd "$(dirname "$package")" && pwd):/artifacts:ro" \
    -v "$SCRIPT_DIR:/smoke:ro" \
    "$image" \
    sh -euxc '
      dnf install -y --setopt=install_weak_deps=False file "/artifacts/$1"
      test -x /usr/bin/crossmacro
      . /smoke/linux-desktop-identity.sh
      crossmacro_validate_native_desktop_identity /
      /smoke/cli-smoke.sh --binary /usr/bin/crossmacro
    ' rpm-container-smoke "$package_name"
}

package=""
image="$DEFAULT_IMAGE"
skip_container=0
install_here=0

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
    --install-in-container)
      install_here=1
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
[ "$((skip_container + install_here))" -le 1 ] || fail "choose one installation mode"
if [ "$install_here" -eq 1 ]; then
  [ "$(id -u)" -eq 0 ] || fail "--install-in-container requires root inside a disposable container"
  [ -f /.dockerenv ] || [ -f /run/.containerenv ] || fail "refusing to install outside a container"
fi

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
assert_contains "RPM dependency" "$requires" "fontconfig"
assert_contains "RPM dependency" "$requires" "libXcursor"
assert_contains "RPM dependency" "$requires" "libXrandr"

assert_payload_path "$payload" "/usr/lib/crossmacro"
assert_payload_path "$payload" "/usr/lib/crossmacro/CrossMacro.UI"
assert_payload_path "$payload" "/usr/lib/crossmacro/daemon/CrossMacro.Daemon"
assert_payload_path "$payload" "/usr/bin/crossmacro"
assert_payload_path "$payload" "/usr/share/applications/CrossMacro.desktop"
assert_payload_path "$payload" "/usr/lib/systemd/system/crossmacro.service"
assert_payload_path "$payload" "/usr/lib/udev/rules.d/99-crossmacro.rules"
assert_payload_path "$payload" "/usr/lib/modules-load.d/crossmacro.conf"
assert_payload_path "$payload" "/usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy"
assert_payload_path "$payload" "/usr/share/polkit-1/rules.d/50-crossmacro.rules"
assert_payload_path "$payload" "/usr/share/selinux/packages/crossmacro/crossmacro.pp"
assert_payload_path "$payload" "/usr/share/licenses/crossmacro/LICENSE"
printf '%s\n' "$payload" | grep -E '^/usr/share/man/man1/crossmacro\.1(\.gz)?$' >/dev/null || fail "payload missing manpage"

if [ "$install_here" -eq 1 ]; then
  # Setup installed the runtime dependencies already. RPM enforces them without network access.
  rpm -Uvh --replacepkgs "$package"
  rpm -q "$APP_NAME"
  getent passwd crossmacro
  getent group crossmacro
  # shellcheck source=scripts/smoke/linux-desktop-identity.sh
  source "$SCRIPT_DIR/linux-desktop-identity.sh"
  crossmacro_validate_native_desktop_identity /
  bash "$CLI_SMOKE" --binary /usr/bin/crossmacro
elif [ "$skip_container" -eq 0 ]; then
  if engine="$(find_container_engine)"; then
    run_container_smoke "$package" "$image" "$engine"
  else
    fail "install smoke requires podman/docker; use --no-container explicitly for static checks only"
  fi
fi

if [ "$skip_container" -eq 1 ]; then
  echo "RPM static checks: OK (installation and CLI not tested)"
else
  echo "RPM package smoke: OK"
fi
