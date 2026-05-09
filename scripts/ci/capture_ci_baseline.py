#!/usr/bin/env python3
"""Capture a read-only baseline of current CI/CD workflow inputs."""

import argparse
import json
import pathlib
import re


def repo_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[2]


def read_lines(path: pathlib.Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines()


def parse_args() -> argparse.Namespace:
    root = repo_root()
    parser = argparse.ArgumentParser(description="Capture the current CI/CD baseline inventory.")
    parser.add_argument(
        "--output",
        type=pathlib.Path,
        required=True,
        help="Write the baseline inventory JSON to this path.",
    )
    parser.add_argument(
        "--pr-check",
        type=pathlib.Path,
        default=root / ".github" / "workflows" / "pr-check.yml",
        help="Path to the PR check workflow.",
    )
    parser.add_argument(
        "--release",
        type=pathlib.Path,
        default=root / ".github" / "workflows" / "release.yml",
        help="Path to the release workflow.",
    )
    return parser.parse_args()


def workflow_name(lines: list[str]) -> str:
    for line in lines:
        match = re.match(r"^name:\s*(.+?)\s*$", line)
        if match:
            return match.group(1)
    return ""


def job_names(lines: list[str]) -> list[str]:
    jobs = []
    in_jobs = False
    for line in lines:
        if re.match(r"^jobs:\s*$", line):
            in_jobs = True
            continue
        if not in_jobs:
            continue
        if re.match(r"^[^\s#].*:\s*$", line):
            break
        match = re.match(r"^  ([A-Za-z0-9_-]+):\s*$", line)
        if match:
            jobs.append(match.group(1))
    return jobs


def collect_artifact_entries(lines: list[str], uses_token: str) -> list[dict[str, str]]:
    entries = []
    current = None
    current_step = ""
    in_with = False
    for line in lines:
        step_match = re.match(r"^\s*- name:\s*(.+?)\s*$", line)
        if step_match:
            current_step = step_match.group(1)
            current = None
            in_with = False
            continue
        if uses_token in line:
            current = {"step": current_step, "uses": line.strip().split("uses:", 1)[1].strip()}
            entries.append(current)
            in_with = False
            continue
        if current is None:
            continue
        if re.match(r"^\s*with:\s*$", line):
            in_with = True
            continue
        if not in_with:
            continue
        name_match = re.match(r"^\s*name:\s*(.+?)\s*$", line)
        if name_match:
            current["artifact_name"] = name_match.group(1)
            continue
        path_match = re.match(r"^\s*path:\s*(.+?)\s*$", line)
        if path_match:
            current["path"] = path_match.group(1)
            continue
    return entries


def collect_release_upload_globs(lines: list[str]) -> list[str]:
    globs = []
    in_release = False
    in_files = False
    for line in lines:
        if re.match(r"^\s*uses:\s*softprops/action-gh-release@", line):
            in_release = True
            in_files = False
            continue
        if not in_release:
            continue
        if re.match(r"^\s*files:\s*(\|)?\s*$", line):
            in_files = True
            continue
        if in_files:
            match = re.match(r"^\s{10,}(.+?)\s*$", line)
            if match:
                globs.append(match.group(1))
                continue
            if re.match(r"^\s*[A-Za-z0-9_-]+:\s*$", line):
                in_files = False
    return globs


def collect_smoke_commands(lines: list[str]) -> list[str]:
    commands = []
    patterns = [
        r"--help",
        r"settings get --json",
        r"run --step",
        r"flatpak run",
        r"makeappx",
        r"hdiutil",
        r"smoke-published-linux-artifacts\.sh",
    ]
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("#"):
            continue
        if any(re.search(pattern, stripped) for pattern in patterns):
            commands.append(stripped)
    return commands


def collect_package_output_globs(pr_lines: list[str], release_lines: list[str]) -> list[str]:
    globs = []
    for line in pr_lines + release_lines:
        match = re.search(r"path:\s*(.+?)\s*$", line)
        if match:
            value = match.group(1)
            if any(token in value for token in ["*", "/", ".deb", ".rpm", ".AppImage", ".flatpak", ".msix", ".dmg", ".exe"]):
                globs.append(value)
        if re.search(r"sha256sum\s+", line):
            match = re.findall(r"([A-Za-z0-9_./*\-]+\.(?:deb|rpm|AppImage|flatpak|exe|dmg))", line)
            globs.extend(match)
    return list(dict.fromkeys(globs))


def manifest_for_workflow(path: pathlib.Path) -> dict:
    lines = read_lines(path)
    return {
        "path": str(path.relative_to(repo_root())),
        "workflow_name": workflow_name(lines),
        "jobs": job_names(lines),
        "artifact_uploads": collect_artifact_entries(lines, "actions/upload-artifact@"),
        "artifact_downloads": collect_artifact_entries(lines, "actions/download-artifact@"),
        "smoke_commands": collect_smoke_commands(lines),
        "release_upload_globs": collect_release_upload_globs(lines),
    }


def main() -> int:
    args = parse_args()
    pr_check = args.pr_check if args.pr_check.is_absolute() else pathlib.Path.cwd() / args.pr_check
    release = args.release if args.release.is_absolute() else pathlib.Path.cwd() / args.release

    pr_lines = read_lines(pr_check.resolve())
    release_lines = read_lines(release.resolve())

    data = {
        "workflows": {
            "pr-check": manifest_for_workflow(pr_check.resolve()),
            "release": manifest_for_workflow(release.resolve()),
        },
        "jobs": {
            "pr-check": job_names(pr_lines),
            "release": job_names(release_lines),
        },
        "package_output_globs": collect_package_output_globs(pr_lines, release_lines),
        "artifact_naming_patterns": {
            "pr-check": {
                "uploads": collect_artifact_entries(pr_lines, "actions/upload-artifact@"),
                "downloads": collect_artifact_entries(pr_lines, "actions/download-artifact@"),
            },
            "release": {
                "uploads": collect_artifact_entries(release_lines, "actions/upload-artifact@"),
                "downloads": collect_artifact_entries(release_lines, "actions/download-artifact@"),
            },
        },
        "smoke_command_inventory": {
            "pr-check": collect_smoke_commands(pr_lines),
            "release": collect_smoke_commands(release_lines),
        },
        "release_upload_globs": collect_release_upload_globs(release_lines),
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Wrote baseline inventory to {args.output}")
    print(f"PR jobs: {', '.join(data['jobs']['pr-check'])}")
    print(f"Release jobs: {', '.join(data['jobs']['release'])}")
    print(f"Release upload globs: {', '.join(data['release_upload_globs'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
