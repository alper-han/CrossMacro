#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
if [ -n "${ACTIONLINT_BINARY:-}" ]; then
    "$ACTIONLINT_BINARY"
else
    lint_dir="$(mktemp -d)"
    trap 'rm -rf "$lint_dir"' EXIT
    curl --fail --silent --show-error --location --retry 3 \
        https://github.com/rhysd/actionlint/releases/download/v1.7.12/actionlint_1.7.12_linux_amd64.tar.gz \
        --output "$lint_dir/actionlint_1.7.12_linux_amd64.tar.gz"
    (cd "$lint_dir" && sha256sum --check "$repo_root/scripts/ci/actionlint.sha256")
    tar -xzf "$lint_dir/actionlint_1.7.12_linux_amd64.tar.gz" -C "$lint_dir" actionlint
    "$lint_dir/actionlint"
fi
mapfile -d '' -t shell_scripts < <(find scripts -type f -name '*.sh' -print0)
for script in "${shell_scripts[@]}"; do
    bash -n "$script"
done
shellcheck --severity=warning --external-sources "${shell_scripts[@]}"
python3 -m unittest discover -s scripts/ci/tests -v
for check in verify-cwd verify-publish verify-flatpak verify-package verify-docs verify-security verify-reusable verify-triggers; do
    args=("$check" --repo-root "$repo_root")
    if [ "$check" = verify-package ]; then args+=(--static-only); fi
    dotnet run --file scripts/ci/CrossMacroCI.cs -- "${args[@]}"
done
