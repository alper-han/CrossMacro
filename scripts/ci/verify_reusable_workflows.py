#!/usr/bin/env python3
"""Validate reusable workflow placement and triggers."""

import argparse
import pathlib
import re

FORBIDDEN_TRIGGERS = {"push", "pull_request", "pull_request_target", "workflow_dispatch"}


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


def indent(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def find_on_block(lines: list[str]) -> tuple[int, int, int] | None:
    for index, raw in enumerate(lines):
        line = strip_comments(raw).rstrip()
        if not line.strip():
            continue
        match = re.match(r"^(\s*)(?:on|'on'|\"on\")\s*:\s*(.*)$", line)
        if not match:
            continue
        block_indent = len(match.group(1))
        inline = match.group(2).strip()
        if inline:
            return index, index + 1, block_indent
        end = len(lines)
        for next_index in range(index + 1, len(lines)):
            next_line = strip_comments(lines[next_index]).rstrip()
            if not next_line.strip():
                continue
            if indent(next_line) <= block_indent:
                end = next_index
                break
        return index, end, block_indent
    return None


def extract_triggers(text: str) -> set[str]:
    lines = text.splitlines()
    block = find_on_block(lines)
    if not block:
        return set()
    start, end, block_indent = block
    inline = strip_comments(lines[start]).split(":", 1)[1].strip()
    if inline.startswith("[") and inline.endswith("]"):
        return {item.strip().strip("'\"") for item in inline[1:-1].split(",") if item.strip()}
    if inline:
        return {inline.strip("'\"")}
    triggers = set()
    for raw in lines[start + 1:end]:
        line = strip_comments(raw).rstrip()
        stripped = line.strip()
        if not stripped or indent(line) != block_indent + 2:
            continue
        if stripped.startswith("-"):
            triggers.add(stripped[1:].strip().strip("'\""))
        elif ":" in stripped:
            triggers.add(stripped.split(":", 1)[0].strip().strip("'\""))
    return {trigger for trigger in triggers if trigger}


def validate(root: pathlib.Path) -> list[str]:
    workflow_dir = root / ".github" / "workflows"
    if not workflow_dir.exists():
        return [f"{workflow_dir}: workflow directory not found"]
    errors = []
    reusable_candidates = sorted(workflow_dir.glob("**/_*.yml")) + sorted(workflow_dir.glob("**/_*.yaml"))
    for path in reusable_candidates:
        relative_parent = path.parent.relative_to(workflow_dir)
        if relative_parent != pathlib.Path("."):
            errors.append(f"{path}: reusable workflows must live directly under .github/workflows, not nested folders")
            continue
        triggers = extract_triggers(path.read_text(encoding="utf-8"))
        if triggers != {"workflow_call"}:
            errors.append(f"{path}: reusable workflows must use only workflow_call, found {sorted(triggers)}")
        forbidden = triggers & FORBIDDEN_TRIGGERS
        if forbidden:
            errors.append(f"{path}: reusable workflow has forbidden trigger(s): {', '.join(sorted(forbidden))}")
    return errors


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate CrossMacro reusable workflow files and placement."
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
    errors = validate(args.repo_root.resolve())
    if errors:
        print("FAIL: reusable workflow policy violations found")
        for error in errors:
            print(f"- {error}")
        return 1
    print("OK: reusable workflow policy validated")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
