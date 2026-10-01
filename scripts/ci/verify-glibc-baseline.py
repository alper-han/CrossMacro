#!/usr/bin/env python3
"""Verify Linux ELF files do not require a newer glibc than the build baseline."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path


GLIBC_VERSION_PATTERN = re.compile(r"\bGLIBC_(\d+(?:\.\d+)+)\b")
ELF_MAGIC = b"\x7fELF"


def parse_version(value: str) -> tuple[int, ...]:
    parts = tuple(int(part) for part in value.split("."))
    if not parts:
        raise ValueError(f"invalid empty version: {value!r}")
    return parts


def extract_glibc_versions(text: str) -> list[tuple[int, ...]]:
    return sorted({parse_version(match) for match in GLIBC_VERSION_PATTERN.findall(text)})


def is_compatible(versions: list[tuple[int, ...]], maximum: tuple[int, ...]) -> bool:
    return not versions or max(versions) <= maximum


def is_elf_file(path: Path) -> bool:
    try:
        with path.open("rb") as stream:
            return stream.read(len(ELF_MAGIC)) == ELF_MAGIC
    except OSError:
        return False


def discover_elf_files(paths: list[Path]) -> list[Path]:
    candidates: list[Path] = []
    for path in paths:
        if path.is_dir():
            candidates.extend(candidate for candidate in path.rglob("*") if candidate.is_file())
        elif path.is_file():
            candidates.append(path)

    return sorted({candidate for candidate in candidates if is_elf_file(candidate)})


def expand_inputs(paths: list[Path]) -> list[Path]:
    expanded: list[Path] = []
    for path in paths:
        if path.is_dir():
            discovered = discover_elf_files([path])
            if not discovered:
                raise RuntimeError(f"directory contains no ELF files: {path}")
            expanded.extend(discovered)
        else:
            expanded.append(path)

    return sorted(set(expanded))


def inspect_binary(path: Path, readelf: str = "readelf") -> list[tuple[int, ...]]:
    if not path.is_file():
        raise RuntimeError(f"binary does not exist: {path}")

    result = subprocess.run(
        [readelf, "--version-info", str(path)],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise RuntimeError(f"readelf failed for {path}: {detail}")
    return extract_glibc_versions(result.stdout + result.stderr)


def format_version(version: tuple[int, ...]) -> str:
    return ".".join(str(part) for part in version)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--max-glibc",
        default="2.35",
        help="maximum permitted glibc symbol version (default: 2.35)",
    )
    parser.add_argument("paths", nargs="+", type=Path)
    args = parser.parse_args(argv)

    maximum = parse_version(args.max_glibc)
    violations: list[str] = []

    try:
        binaries = expand_inputs(args.paths)
    except RuntimeError as error:
        binaries = []
        violations.append(str(error))

    for binary in binaries:
        try:
            versions = inspect_binary(binary)
        except RuntimeError as error:
            violations.append(str(error))
            continue

        if versions:
            highest = max(versions)
            print(f"{binary}: highest GLIBC requirement is GLIBC_{format_version(highest)}")
        else:
            highest = None
            print(f"{binary}: no GLIBC symbol versions found")

        if highest is not None and not is_compatible(versions, maximum):
            violations.append(
                f"{binary}: requires GLIBC_{format_version(highest)}, "
                f"but the maximum allowed version is GLIBC_{args.max_glibc}"
            )

    if violations:
        print("GLIBC baseline check failed:", file=sys.stderr)
        for violation in violations:
            print(f"- {violation}", file=sys.stderr)
        return 1

    print(f"GLIBC baseline check passed (maximum GLIBC_{args.max_glibc})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
