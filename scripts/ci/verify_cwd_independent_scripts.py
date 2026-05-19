#!/usr/bin/env python3
"""Cheap CWD-independence checks for packaging scripts."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class CheckResult:
    script: str
    cwd: Path
    ok: bool
    message: str


SHELL_WRAPPERS = [
    "scripts/build_deb.sh",
    "scripts/build_rpm.sh",
    "scripts/build_appimage.sh",
    "scripts/build_flatpak.sh",
    "scripts/build_macos.sh",
]

SHELL_IMPLEMENTATIONS = [
    "scripts/packaging/deb/build.sh",
    "scripts/packaging/rpm/build.sh",
    "scripts/packaging/appimage/build.sh",
    "scripts/packaging/flatpak/build.sh",
    "scripts/packaging/macos/build.sh",
]

SHELL_SCRIPTS = SHELL_WRAPPERS + SHELL_IMPLEMENTATIONS

POWERSHELL_WRAPPERS = [
    "scripts/msix/build-msix.ps1",
    "scripts/msix/build-msix-store-upload.ps1",
    "scripts/msix/prepare-msix.ps1",
]

POWERSHELL_IMPLEMENTATIONS = [
    "scripts/packaging/msix/build-msix.ps1",
    "scripts/packaging/msix/build-msix-store-upload.ps1",
    "scripts/packaging/msix/prepare-msix.ps1",
]

PACKAGE_SCRIPTS = SHELL_SCRIPTS + POWERSHELL_WRAPPERS + POWERSHELL_IMPLEMENTATIONS

RELATIVE_REPO_PATH_PATTERNS = [
    re.compile(r"(?<![A-Za-z0-9_$/{.-])(?:\.\./)?src/"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])(?:\.\./)?docs/"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])assets/"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])daemon/"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])flatpak/"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])CrossMacro\.sln"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])README\.md"),
    re.compile(r"(?<![A-Za-z0-9_$/{.-])LICENSE"),
]

EXPECTED_SHELL_WRAPPER_TARGETS = {
    "scripts/build_deb.sh": "packaging/deb/build.sh",
    "scripts/build_rpm.sh": "packaging/rpm/build.sh",
    "scripts/build_appimage.sh": "packaging/appimage/build.sh",
    "scripts/build_flatpak.sh": "packaging/flatpak/build.sh",
    "scripts/build_macos.sh": "packaging/macos/build.sh",
}

EXPECTED_SHELL_PATHS = {
    "scripts/packaging/deb/build.sh": [
        'SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"',
        'PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"',
        'PUBLISH_DIR="${PUBLISH_DIR:-$SCRIPTS_DIR/../publish}"',
        'DEB_DIR="$SCRIPTS_DIR/deb_package"',
        'OUTPUT_DEB="$SCRIPTS_DIR/${APP_NAME}-${DEB_VERSION}_${ARCH}.deb"',
        'dotnet publish "$PROJECT_ROOT/src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"',
        'cp "$SCRIPTS_DIR/assets/CrossMacro.desktop"',
        'MANPAGE_SOURCE="$PROJECT_ROOT/docs/man/crossmacro.1"',
    ],
    "scripts/packaging/rpm/build.sh": [
        'SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"',
        'PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"',
        'PUBLISH_DIR="${PUBLISH_DIR:-$SCRIPTS_DIR/../publish}"',
        'RPM_BUILD_DIR="$SCRIPTS_DIR/rpm_build"',
        'dotnet publish "$PROJECT_ROOT/src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"',
        'cp "$SCRIPTS_DIR/packaging/rpm/crossmacro.spec"',
        'cp "$SCRIPTS_DIR/assets/CrossMacro.desktop"',
        'cp "$RPM_BUILD_DIR"/RPMS/"$RPM_ARCH"/*.rpm "$SCRIPTS_DIR/"',
    ],
    "scripts/packaging/appimage/build.sh": [
        'SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"',
        'PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"',
        'PUBLISH_DIR="${PUBLISH_DIR:-$SCRIPTS_DIR/../publish}"',
        'APP_DIR="$SCRIPTS_DIR/AppDir"',
        'APPIMAGETOOL_PATH="$SCRIPTS_DIR/$APPIMAGETOOL_NAME"',
        'APPIMAGE_OUTPUT="$SCRIPTS_DIR/CrossMacro-${PACKAGE_VERSION}-${APPIMAGE_ARCH}.AppImage"',
        'cp "$PROJECT_ROOT/src/CrossMacro.UI/Assets/icons/512x512/apps/crossmacro.png"',
        'cp "$SCRIPTS_DIR/assets/$APP_NAME.desktop"',
    ],
    "scripts/packaging/flatpak/build.sh": [
        'SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"',
        'PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"',
        'FLATPAK_DIR="$PROJECT_ROOT/flatpak"',
        'BUILD_DIR="$SCRIPTS_DIR/flatpak-source"',
    ],
    "scripts/packaging/macos/build.sh": [
        'SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"',
        'PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"',
        'OUTPUT_DIR="${OUTPUT_DIR:-$SCRIPTS_DIR/macos_output}"',
        'dotnet publish "$PROJECT_ROOT/src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj"',
        'ICON_PNG="$PROJECT_ROOT/src/CrossMacro.UI/Assets/mouse-icon.png"',
    ],
}

EXPECTED_PS_WRAPPER_TARGETS = {
    "scripts/msix/build-msix.ps1": "../packaging/msix/build-msix.ps1",
    "scripts/msix/build-msix-store-upload.ps1": "../packaging/msix/build-msix-store-upload.ps1",
    "scripts/msix/prepare-msix.ps1": "../packaging/msix/prepare-msix.ps1",
}

EXPECTED_PS_IMPLEMENTATION_PATHS = {
    "scripts/packaging/msix/build-msix.ps1": [
        "$scriptsDir = (Resolve-Path -LiteralPath (Join-Path $scriptDir '../..')).Path",
        "$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptsDir '..')).Path",
        "$manifestPath = Join-Path $scriptsDir 'msix/AppxManifest.xml'",
        "$assetsPath = Join-Path $scriptsDir 'msix/Assets'",
    ],
    "scripts/packaging/msix/build-msix-store-upload.ps1": [
        "$scriptsDir = (Resolve-Path -LiteralPath (Join-Path $scriptDir '../..')).Path",
        "$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptsDir '..')).Path",
        "$smokeScript = Join-Path $projectRoot 'scripts/smoke/msix.ps1'",
    ],
    "scripts/packaging/msix/prepare-msix.ps1": [
        '$ScriptsDir = Resolve-Path -LiteralPath (Join-Path $ScriptDir "../..")',
        '$ProjectRoot = Resolve-Path -LiteralPath (Join-Path $ScriptsDir "..")',
        '$ManifestPath = Join-Path $ScriptsDir "msix/AppxManifest.xml"',
        '$AssetsPath = Join-Path $ScriptsDir "msix/Assets"',
    ],
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="Repository root to verify. Defaults to this script's repository.",
    )
    return parser.parse_args()


def run_command(command: list[str], cwd: Path) -> tuple[bool, str]:
    proc = subprocess.run(
        command,
        cwd=str(cwd),
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    output = proc.stdout.strip()
    return proc.returncode == 0, output


def strip_comments(line: str) -> str:
    if line.lstrip().startswith("#"):
        return ""
    return line


def has_unanchored_repo_paths(text: str) -> list[str]:
    failures: list[str] = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        code = strip_comments(line)
        if not code:
            continue
        for pattern in RELATIVE_REPO_PATH_PATTERNS:
            if pattern.search(code):
                if any(anchor in code for anchor in (
                    "$PROJECT_ROOT",
                    "$SCRIPTS_DIR",
                    "$projectRoot",
                    "$scriptsDir",
                    "$ProjectRoot",
                    "$ScriptsDir",
                )):
                    continue
                failures.append(f"line {line_number}: {line.strip()}")
                break
    return failures


def make_static_result(script: str, details: list[str], ok_message: str) -> CheckResult:
    return CheckResult(
        script=script,
        cwd=Path.cwd(),
        ok=not details,
        message="; ".join(details) if details else ok_message,
    )


def verify_shell_wrappers(repo: Path) -> list[CheckResult]:
    results: list[CheckResult] = []
    for script, target in EXPECTED_SHELL_WRAPPER_TARGETS.items():
        text = (repo / script).read_text(encoding="utf-8")
        expected = [
            'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"',
            f'exec "$SCRIPT_DIR/{target}" "$@"',
        ]
        details = ["missing wrapper delegation: " + item for item in expected if item not in text]
        results.append(make_static_result(script, details, "wrapper delegates with argument passthrough"))
    return results


def verify_shell_static(repo: Path) -> list[CheckResult]:
    results: list[CheckResult] = []
    for script in SHELL_IMPLEMENTATIONS:
        text = (repo / script).read_text(encoding="utf-8")
        missing = [expected for expected in EXPECTED_SHELL_PATHS[script] if expected not in text]
        unanchored = has_unanchored_repo_paths(text)
        details: list[str] = []
        if missing:
            details.append("missing expected anchors: " + "; ".join(missing))
        if unanchored:
            details.append("unanchored repo paths: " + "; ".join(unanchored))
        results.append(make_static_result(script, details, "static path anchors present"))
    return results


def verify_powershell_wrappers(repo: Path) -> list[CheckResult]:
    results: list[CheckResult] = []
    for script, target in EXPECTED_PS_WRAPPER_TARGETS.items():
        text = (repo / script).read_text(encoding="utf-8-sig")
        expected = [
            "$ScriptDir = if ($PSScriptRoot)",
            f"$TargetScript = Join-Path $ScriptDir '{target}'",
            "$forwardArgs = @",
            "& $TargetScript @forwardArgs",
            "exit $LASTEXITCODE",
        ]
        details = ["missing wrapper delegation: " + item for item in expected if item not in text]
        results.append(make_static_result(script, details, "wrapper delegates with argument passthrough"))
    return results


def verify_powershell_static(repo: Path) -> list[CheckResult]:
    results: list[CheckResult] = []
    for script, expected_paths in EXPECTED_PS_IMPLEMENTATION_PATHS.items():
        text = (repo / script).read_text(encoding="utf-8-sig")
        missing = [expected for expected in expected_paths if expected not in text]
        unanchored = has_unanchored_repo_paths(text)
        details: list[str] = []
        if missing:
            details.append("missing expected anchors: " + "; ".join(missing))
        if unanchored:
            details.append("unanchored repo paths: " + "; ".join(unanchored))
        results.append(make_static_result(script, details, "static path anchors present"))
    return results


def verify_bash_syntax(repo: Path, cwd: Path) -> CheckResult:
    command = ["bash", "-n", *[str(repo / script) for script in SHELL_SCRIPTS]]
    ok, output = run_command(command, cwd)
    return CheckResult(
        script="bash -n package scripts",
        cwd=cwd,
        ok=ok,
        message=output or "syntax ok",
    )


def verify_repo_root(repo: Path) -> None:
    required = [repo / script for script in PACKAGE_SCRIPTS]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise SystemExit("Missing expected scripts:\n" + "\n".join(missing))


def print_results(results: list[CheckResult]) -> bool:
    all_ok = True
    for result in results:
        status = "PASS" if result.ok else "FAIL"
        print(f"[{status}] {result.script} (cwd={result.cwd}) - {result.message}")
        all_ok = all_ok and result.ok
    return all_ok


def main() -> int:
    args = parse_args()
    repo = args.repo.resolve()
    verify_repo_root(repo)

    results: list[CheckResult] = []
    with tempfile.TemporaryDirectory(prefix="crossmacro-cwd-verify-") as temp_dir:
        cwd_cases = [repo, Path(temp_dir)]
        for cwd in cwd_cases:
            results.append(verify_bash_syntax(repo, cwd))

    results.extend(verify_shell_wrappers(repo))
    results.extend(verify_shell_static(repo))
    results.extend(verify_powershell_wrappers(repo))
    results.extend(verify_powershell_static(repo))

    return 0 if print_results(results) else 1


if __name__ == "__main__":
    sys.exit(main())
