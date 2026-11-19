#!/usr/bin/env bash
# Argument-parsing helpers shared by scripts/ci/*.sh.

fail() {
    echo "Error: $*" >&2
    exit 1
}

# require_value "$@": call from an option's case branch to ensure "$2" is a non-empty value.
require_value() {
    if [ "$#" -lt 2 ] || [ -z "${2:-}" ]; then
        fail "missing value for $1"
    fi
}
