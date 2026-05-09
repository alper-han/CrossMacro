#!/usr/bin/env python3
"""Validate staged release artifact files against the expected asset manifest."""

import argparse
import copy
import json
import pathlib
import re
from typing import Optional


def repo_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[2]


def parse_bool(value: str) -> bool:
    normalized = value.strip().lower()
    if normalized in {"true", "1", "yes", "y", "on"}:
        return True
    if normalized in {"false", "0", "no", "n", "off"}:
        return False
    raise argparse.ArgumentTypeError(f"expected true or false, got {value!r}")


def load_manifest(path: pathlib.Path) -> dict:
    try:
        with path.open("r", encoding="utf-8") as handle:
            manifest = json.load(handle)
    except FileNotFoundError as exc:
        raise ValueError(f"manifest not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ValueError(f"manifest is not valid JSON: {path}: {exc}") from exc
    if not isinstance(manifest, dict):
        raise ValueError("manifest root must be a JSON object")
    assets = manifest.get("assets")
    if not isinstance(assets, list):
        raise ValueError("manifest must contain an assets array")
    return manifest


def rewrite_manifest_for_version(manifest: dict, version: Optional[str]) -> dict:
    if version is None:
        return manifest

    sample_version = manifest.get("sampleVersion")
    if sample_version == version:
        return manifest

    rewritten = copy.deepcopy(manifest)
    rewritten["sampleVersion"] = version
    release_tag = rewritten.get("releaseTag")
    if isinstance(release_tag, str) and release_tag.startswith("v"):
        rewritten["releaseTag"] = f"v{version}"

    for asset in rewritten.get("assets", []):
        if not isinstance(asset, dict):
            continue
        file_name = asset.get("file")
        if isinstance(file_name, str):
            asset["file"] = rewrite_asset_file_name(asset, file_name, str(sample_version), version)

    return rewritten


def parse_semver_parts(version: str) -> tuple[str, str]:
    base = re.split(r"[-+]", version, maxsplit=1)[0]
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", base):
        raise ValueError(f"invalid semantic base version {base!r} from {version!r}")
    prerelease = ""
    if "-" in version:
        prerelease = version.split("-", 1)[1].split("+", 1)[0]
    return base, prerelease


def normalize_token(token: str, kind: str) -> str:
    if kind == "deb":
        value = re.sub(r"[^0-9A-Za-z.+~-]", ".", token)
    elif kind in {"rpm", "aur"}:
        value = re.sub(r"[-+]", ".", token)
        value = re.sub(r"[^0-9A-Za-z._]", ".", value)
    elif kind == "filename":
        value = re.sub(r"[^0-9A-Za-z._+-]", ".", token)
    else:
        value = token
    value = re.sub(r"\.+", ".", value)
    return value.strip(".")


def to_deb_version(version: str) -> str:
    base, prerelease = parse_semver_parts(version)
    if not prerelease:
        return base
    return f"{base}~{normalize_token(prerelease, 'deb') or 'pre'}"


def to_rpm_version(version: str) -> str:
    base, _ = parse_semver_parts(version)
    return base


def to_rpm_release(version: str) -> str:
    _, prerelease = parse_semver_parts(version)
    if not prerelease:
        return "1"
    return f"0.1.{normalize_token(prerelease, 'rpm') or 'pre'}"


def to_filename_version(version: str) -> str:
    value = normalize_token(version, "filename")
    if not value:
        raise ValueError(f"failed to normalize filename version from {version!r}")
    return value


def rewrite_asset_file_name(asset: dict, file_name: str, sample_version: str, version: str) -> str:
    kind = asset.get("kind")
    if kind == "deb":
        return file_name.replace(to_deb_version(sample_version), to_deb_version(version))
    if kind == "rpm":
        sample_rpm = f"{to_rpm_version(sample_version)}-{to_rpm_release(sample_version)}"
        version_rpm = f"{to_rpm_version(version)}-{to_rpm_release(version)}"
        return file_name.replace(sample_rpm, version_rpm)
    return file_name.replace(to_filename_version(sample_version), to_filename_version(version))


def expected_assets(manifest: dict, attach_flatpak: bool, attach_msix: bool) -> list[dict]:
    assets = []
    toggles = {
        "attach_flatpak": attach_flatpak,
        "attach_msix": attach_msix,
    }
    for asset in manifest["assets"]:
        if not isinstance(asset, dict):
            continue
        if asset.get("enabledByDefault") is not True:
            continue
        manual_input = asset.get("manualInput")
        if manual_input in toggles and not toggles[manual_input]:
            continue
        assets.append(asset)
    return assets


def validate(manifest_path: pathlib.Path, directory: pathlib.Path, attach_flatpak: bool, attach_msix: bool, version: Optional[str]) -> list[str]:
    errors = []
    try:
        manifest = load_manifest(manifest_path)
    except ValueError as exc:
        return [str(exc)]
    manifest = rewrite_manifest_for_version(manifest, version)
    assets = expected_assets(manifest, attach_flatpak, attach_msix)
    if not directory.exists():
        errors.append(f"artifact directory not found: {directory}")
    elif not directory.is_dir():
        errors.append(f"artifact path is not a directory: {directory}")
    expected_names: set[str] = set()
    for asset in assets:
        file_name = asset.get("file")
        if not isinstance(file_name, str) or not file_name:
            errors.append(f"manifest asset missing non-empty file field: {asset}")
            continue
        expected_names.add(file_name)
        if not (directory / file_name).is_file():
            details = []
            for key in ("kind", "platform", "arch"):
                value = asset.get(key)
                if value:
                    details.append(f"{key}={value}")
            suffix = f" ({', '.join(details)})" if details else ""
            errors.append(f"missing artifact: {file_name}{suffix}")
    if directory.is_dir():
        actual_names = {p.name for p in directory.iterdir() if p.is_file()}
        for extra in sorted(actual_names - expected_names):
            errors.append(f"unexpected artifact not in manifest: {extra}")
    return errors


def parse_args() -> argparse.Namespace:
    root = repo_root()
    parser = argparse.ArgumentParser(
        description="Validate a release artifact directory against scripts/ci/expected-release-assets.json."
    )
    parser.add_argument(
        "--manifest",
        type=pathlib.Path,
        default=root / "scripts" / "ci" / "expected-release-assets.json",
        help="Expected release asset manifest JSON.",
    )
    parser.add_argument(
        "--directory",
        type=pathlib.Path,
        required=True,
        help="Directory containing staged release artifact files.",
    )
    parser.add_argument(
        "--attach-flatpak",
        type=parse_bool,
        default=True,
        metavar="true|false",
        help="Require Flatpak assets from the manifest. Defaults to true.",
    )
    parser.add_argument(
        "--attach-msix",
        type=parse_bool,
        default=True,
        metavar="true|false",
        help="Require MSIX assets from the manifest. Defaults to true.",
    )
    parser.add_argument(
        "--version",
        help="Override the manifest sample version when validating staged artifact names.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = args.manifest if args.manifest.is_absolute() else pathlib.Path.cwd() / args.manifest
    directory = args.directory if args.directory.is_absolute() else pathlib.Path.cwd() / args.directory
    errors = validate(manifest.resolve(), directory.resolve(), args.attach_flatpak, args.attach_msix, args.version)
    if errors:
        print("FAIL: release artifact validation failed")
        for error in errors:
            print(f"- {error}")
        return 1
    print("OK: release artifact directory contains every required enabled asset")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
