#!/usr/bin/env bash
# Shared helpers for scripts/smoke/*.sh.
#
# Source this file after `set -euo pipefail` and set SMOKE_LABEL (for example
# "DEB") so failures read "<label> smoke failed: <message>".

fail() {
  echo "${SMOKE_LABEL:?SMOKE_LABEL must be set before sourcing scripts/lib/test.sh} smoke failed: $1" >&2
  if [ "$#" -gt 1 ] && [ -n "${2:-}" ]; then
    printf '%s\n' "$2" >&2
  fi
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

# assert_contains <name> <haystack> <needle>: fixed-string match; the haystack is
# echoed on failure so the offending output is visible in CI logs.
assert_contains() {
  printf '%s\n' "$2" | grep -F -- "$3" >/dev/null || fail "$1 missing: $3" "$2"
}

# Prints the container engine to use: $CONTAINER_ENGINE when set, else podman, else docker.
find_container_engine() {
  if [ -n "${CONTAINER_ENGINE:-}" ]; then
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
