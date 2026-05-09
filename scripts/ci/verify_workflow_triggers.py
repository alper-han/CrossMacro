#!/usr/bin/env python3
"""Validate GitHub Actions workflow trigger policy for CrossMacro."""

import argparse
import pathlib
import re

TARGET_WORKFLOWS = {
    "ci.yml": {"push", "pull_request"},
    "package-linux.yml": {"push", "pull_request"},
    "package-windows.yml": {"push", "pull_request"},
    "package-macos.yml": {"push", "pull_request"},
    "release.yml": {"workflow_dispatch"},
}
FORBIDDEN_TRIGGERS = {"pull_request_target"}
TRIGGER_KEYS = {
    "push",
    "pull_request",
    "pull_request_target",
    "workflow_dispatch",
    "workflow_call",
    "schedule",
    "release",
    "repository_dispatch",
    "workflow_run",
    "merge_group",
}


def repo_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[2]


def strip_comments(line: str) -> str:
    in_single = False
    in_double = False
    for index, char in enumerate(line):
        if char == "'" and not in_double:
            in_single = not in_single
        elif char == '"' and not in_single:
            in_double = not in_double
        elif char == "#" and not in_single and not in_double:
            return line[:index]
    return line


def line_indent(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def find_on_block(lines: list[str]) -> tuple[int, int, int] | None:
    for index, raw in enumerate(lines):
        line = strip_comments(raw).rstrip()
        if not line.strip():
            continue
        match = re.match(r"^(\s*)(?:on|'on'|\"on\")\s*:\s*(.*)$", line)
        if not match:
            continue
        indent = len(match.group(1))
        inline = match.group(2).strip()
        if inline:
            return index, index + 1, indent
        end = len(lines)
        for next_index in range(index + 1, len(lines)):
            next_line = strip_comments(lines[next_index]).rstrip()
            if not next_line.strip():
                continue
            if line_indent(next_line) <= indent:
                end = next_index
                break
        return index, end, indent
    return None


def parse_inline_triggers(value: str) -> set[str]:
    value = value.strip()
    if value.startswith("[") and value.endswith("]"):
        return {item.strip().strip("'\"") for item in value[1:-1].split(",") if item.strip()}
    if value:
        return {value.strip("'\"")}
    return set()


def extract_triggers(text: str) -> set[str]:
    lines = text.splitlines()
    block = find_on_block(lines)
    if block is None:
        return set()
    start, end, indent = block
    on_line = strip_comments(lines[start]).rstrip()
    inline_value = on_line.split(":", 1)[1].strip()
    if inline_value:
        return parse_inline_triggers(inline_value)
    triggers = set()
    for raw in lines[start + 1:end]:
        line = strip_comments(raw).rstrip()
        stripped = line.strip()
        if not stripped:
            continue
        if line_indent(line) != indent + 2:
            continue
        if stripped.startswith("-"):
            item = stripped[1:].strip().strip("'\"")
            if item:
                triggers.add(item)
            continue
        key = stripped.split(":", 1)[0].strip().strip("'\"")
        if key:
            triggers.add(key)
    return triggers


def push_has_tags(text: str) -> bool:
    return bool(re.search(r"(?ms)^\s{2,}push\s*:\s*$.*?^\s{4,}tags\s*:", text))


def workflow_creates_release(text: str) -> bool:
    release_patterns = [
        r"softprops/action-gh-release",
        r"gh\s+release\s+(?:create|upload|edit)",
        r"actions/create-release",
        r"ncipollo/release-action",
        r"github\.rest\.repos\.(?:createRelease|uploadReleaseAsset|updateRelease)",
    ]
    return any(re.search(pattern, text) for pattern in release_patterns)


def push_uses_all_branches(text: str) -> bool:
    return bool(re.search(r"(?ms)^\s{2,}push\s*:\s*$.*?^\s{4,}branches\s*:\s*\[\s*['\"]\*\*['\"]\s*\]", text))


def validate_workflow(path: pathlib.Path, expected: set[str] | None) -> list[str]:
    errors = []
    text = path.read_text(encoding="utf-8")
    triggers = extract_triggers(text)
    if not triggers:
        errors.append(f"{path}: missing top-level 'on' workflow triggers")
        return errors
    forbidden = triggers & FORBIDDEN_TRIGGERS
    if forbidden:
        errors.append(f"{path}: forbidden trigger(s): {', '.join(sorted(forbidden))}")
    if expected is not None:
        if triggers != expected:
            errors.append(
                f"{path}: expected triggers {sorted(expected)}, found {sorted(triggers)}"
            )
        if path.name == "release.yml" and "push" in triggers:
            errors.append(f"{path}: release.yml must be workflow_dispatch only; tag push cannot create releases")
        if path.name in {"ci.yml", "package-linux.yml", "package-windows.yml", "package-macos.yml"} and "push" in triggers and not push_uses_all_branches(text):
            errors.append(f"{path}: branch push validation must run on all branches with branches: ['**']")
        if path.name != "release.yml" and "push" in triggers and "pull_request" not in triggers:
            errors.append(f"{path}: branch push validation workflows must also run on pull_request")
    else:
        unknown_forbidden = triggers & FORBIDDEN_TRIGGERS
        if unknown_forbidden:
            errors.append(f"{path}: forbidden trigger(s): {', '.join(sorted(unknown_forbidden))}")
    if "push" in triggers and push_has_tags(text) and workflow_creates_release(text):
        errors.append(f"{path}: tag push path appears able to create or upload release assets")
    return errors


def discover_workflows(root: pathlib.Path) -> list[pathlib.Path]:
    workflows = root / ".github" / "workflows"
    if not workflows.exists():
        return []
    return sorted(list(workflows.glob("*.yml")) + list(workflows.glob("*.yaml")))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate CrossMacro GitHub Actions workflow trigger policy."
    )
    parser.add_argument(
        "--workflow",
        type=pathlib.Path,
        help="Validate one workflow file strictly instead of the future target workflow set.",
    )
    parser.add_argument(
        "--repo-root",
        type=pathlib.Path,
        default=repo_root(),
        help="Repository root. Defaults to discovery from this script location.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.repo_root.resolve()
    errors = []
    if args.workflow:
        workflow = args.workflow if args.workflow.is_absolute() else root / args.workflow
        if not workflow.exists():
            print(f"FAIL: workflow not found: {workflow}")
            return 1
        expected = TARGET_WORKFLOWS.get(workflow.name)
        errors.extend(validate_workflow(workflow, expected))
    else:
        workflow_dir = root / ".github" / "workflows"
        for name, expected in TARGET_WORKFLOWS.items():
            path = workflow_dir / name
            if not path.exists():
                errors.append(f"{path}: missing future target workflow required by CI/CD contract")
                continue
            errors.extend(validate_workflow(path, expected))
        for workflow in discover_workflows(root):
            if workflow.name not in TARGET_WORKFLOWS:
                errors.extend(validate_workflow(workflow, None))
    if errors:
        print("FAIL: workflow trigger policy violations found")
        for error in errors:
            print(f"- {error}")
        return 1
    print("OK: workflow trigger policy validated")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
