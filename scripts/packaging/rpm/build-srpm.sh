#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$PROJECT_ROOT/scripts/lib/version.sh"
# shellcheck source=scripts/lib/platform.sh
source "$PROJECT_ROOT/scripts/lib/platform.sh"
# shellcheck source=scripts/packaging/rpm/common.sh
source "$SCRIPT_DIR/common.sh"
OUTDIR="${CROSSMACRO_ARTIFACT_ROOT:-$PROJECT_ROOT/artifacts}/packages/srpm"
SPEC="$SCRIPT_DIR/crossmacro.spec"
while [ "$#" -gt 0 ]; do
    case "$1" in
        --outdir|--spec)
            [ "$#" -ge 2 ] && [ -n "$2" ] || { echo "Missing value for $1" >&2; exit 2; }
            if [ "$1" = --outdir ]; then OUTDIR="$2"; else SPEC="$2"; fi
            shift 2 ;;
        --help|-h) echo 'Usage: build-srpm.sh [--outdir DIR] [--spec PATH]'; exit 0 ;;
        *) echo 'Unknown SRPM option' >&2; exit 2 ;;
    esac
done
case "$SPEC" in /*) ;; *) SPEC="$PROJECT_ROOT/$SPEC" ;; esac
SPEC="$(realpath -m -- "$SPEC")"
OUTDIR="$(realpath -m -- "$OUTDIR")"
case "$SPEC" in "$PROJECT_ROOT"/*) ;; *) echo 'Spec must belong to the checkout' >&2; exit 2;; esac
[ -f "$SPEC" ] || { echo 'Spec file is missing' >&2; exit 2; }
assert_safe_linux_work_dir "$OUTDIR" "$PROJECT_ROOT"
for command in git tar gzip sort realpath sed rpmbuild; do
    command -v "$command" >/dev/null || { echo "Missing SRPM tool: $command" >&2; exit 1; }
done
work="$(mktemp -d /tmp/crossmacro-srpm.XXXXXX)"
trap 'rm -rf -- "$work"' EXIT
stage_root="$work/snapshot"
mkdir -p "$stage_root" "$work/SPECS" "$work/SOURCES" "$work/SRPMS"

source_paths=(VERSION global.json 'Directory.*' NuGet.Config nuget.config CrossMacro.sln
              src native scripts/lib scripts/ci/publish-linux-artifacts.sh
              scripts/packaging/rpm .copr/Makefile "${RPM_ASSET_PATHS[@]}")
git -C "$PROJECT_ROOT" ls-files -z --cached --others --exclude-standard \
    -- "${source_paths[@]}" | LC_ALL=C sort -zu > "$work/candidates"
while IFS= read -r -d '' relative; do
    case "$relative" in
        /*|../*|*/../*|*/..) echo 'Unsafe source path' >&2; exit 1 ;;
        .git/*|*/.git/*|*/bin/*|*/obj/*|*/__pycache__/*|artifacts/*|*/artifacts/*|publish/*|*/publish/*|dist/*|*/dist/*|.env*|*/.env*) continue ;;
    esac
    source_path="$PROJECT_ROOT/$relative"
    [ "$(realpath -m -- "$source_path")" = "$source_path" ] && [ ! -L "$source_path" ] \
        || { echo 'Symlinked source input is not self-contained' >&2; exit 1; }
    [ -e "$source_path" ] || continue
    [ -f "$source_path" ] || { echo 'Non-file source input' >&2; exit 1; }
    mkdir -p -- "$stage_root/$(dirname -- "$relative")"
    cp -p -- "$source_path" "$stage_root/$relative"
done < "$work/candidates"

for required in VERSION global.json Directory.Build.props Directory.Build.targets Directory.Packages.props \
                scripts/ci/publish-linux-artifacts.sh scripts/lib/version.sh scripts/lib/platform.sh; do
    [ -f "$stage_root/$required" ] || { echo "Missing source input: $required" >&2; exit 1; }
done
for relative in "${RPM_ASSET_PATHS[@]}"; do
    [ -e "$stage_root/$relative" ] || { echo "Missing RPM asset: $relative" >&2; exit 1; }
done
file_version="$(read_version_file "$stage_root")"
if [ -n "${VERSION:-}" ] && [ "$VERSION" != "$file_version" ]; then
    echo 'VERSION differs from archived VERSION' >&2; exit 1
fi
VERSION="$file_version"
canonical="$(get_canonical_package_version)"
RPM_VERSION="$(to_rpm_version "$canonical")"
RPM_RELEASE="$(to_rpm_release "$canonical")"
[ "$RPM_VERSION" = "$file_version" ] || { echo 'Package version differs from archived VERSION' >&2; exit 1; }
mv -- "$stage_root" "$work/crossmacro-$RPM_VERSION"
render_rpm_spec "$SPEC" "$work/SPECS/crossmacro.spec" "$RPM_VERSION" "$RPM_RELEASE"
write_rpm_source0 "$work" "$RPM_VERSION" "$work/SOURCES"
rpmbuild -bs --nodeps --define "_topdir $work" "$work/SPECS/crossmacro.spec"
mkdir -p -- "$OUTDIR"
cp -- "$work/SRPMS/crossmacro-$RPM_VERSION-$RPM_RELEASE.src.rpm" "$OUTDIR/"
