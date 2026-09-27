#!/usr/bin/env bash

# Consumed by both sourcing entry points, not within this library.
# shellcheck disable=SC2034
RPM_ASSET_PATHS=(
    LICENSE docs/man/crossmacro.1 scripts/daemon/crossmacro.service
    scripts/packaging/rpm/crossmacro.te scripts/assets/CrossMacro.desktop
    scripts/assets/99-crossmacro.rules scripts/assets/50-crossmacro.rules
    scripts/assets/crossmacro-modules.conf scripts/assets/io.github.alper_han.crossmacro.policy
    scripts/assets/io.github.alper_han.crossmacro.metainfo.xml src/CrossMacro.UI/Assets/icons
)

render_rpm_spec() {
    sed -e "s/^Version:.*/Version:        $3/" \
        -e "s/^Release:.*/Release:        $4/" "$1" > "$2"
}

write_rpm_source0() {
    tar -C "$1" -czf "$3/crossmacro-$2.tar.gz" -- "crossmacro-$2"
}
