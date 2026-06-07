#!/usr/bin/env python3
"""Validate GitHub Actions workflow permission and secret policy."""

import argparse
import pathlib
import re

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
RELEASE_WORKFLOW = "release.yml"
PAGES_WORKFLOW = "pages.yml"
RELEASE_WRITE_JOB = "create-release"
PUBLISH_JOB_GATES = {
    "update-aur": "publish_aur",
    "publish-winget": "publish_winget",
}
EXTERNAL_PUBLISH_INTENT_INPUTS = ("publish_release", "publish_existing_release")
FULL_COMMIT_SHA_REF = re.compile(r"@[0-9a-fA-F]{40}\s*(?:#.*)?$")
MUTABLE_ACTION_REF = re.compile(r"(?m)^\s*uses\s*:\s*([^\s#]+)\s*(?:#.*)?$")
AUR_ED25519_FINGERPRINT = "SHA256:RFzBCUItH9LZS0cKB5UE6ceAYhBD5C8GeOBip8Z11+4"
SECRET_PUBLISH_ACTIONS_REQUIRING_SHA = ()
MUTABLE_EXECUTABLE_DOWNLOADS = (
    "aka.ms/wingetcreate/latest",
)


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


def find_block(lines: list[str], key: str, base_indent: int = 0) -> tuple[int, int, int] | None:
    pattern = re.compile(rf"^(\s*){re.escape(key)}\s*:\s*(.*)$")
    for index, raw in enumerate(lines):
        line = strip_comments(raw).rstrip()
        if not line.strip():
            continue
        match = pattern.match(line)
        if not match:
            continue
        block_indent = len(match.group(1))
        if block_indent != base_indent:
            continue
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


def extract_on_triggers(lines: list[str]) -> set[str]:
    block = None
    for spelling in ("on", "'on'", '"on"'):
        block = find_block(lines, spelling, 0)
        if block:
            break
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
        else:
            triggers.add(stripped.split(":", 1)[0].strip().strip("'\""))
    return {trigger for trigger in triggers if trigger}


def top_level_permissions(lines: list[str]) -> tuple[dict[str, str], bool]:
    return permissions_at_indent(lines, 0)


def permissions_at_indent(lines: list[str], base_indent: int) -> tuple[dict[str, str], bool]:
    block = find_block(lines, "permissions", base_indent)
    if not block:
        return {}, False
    start, end, block_indent = block
    inline = strip_comments(lines[start]).split(":", 1)[1].strip()
    if inline:
        return {"__inline__": inline.strip()}, True
    values = {}
    for raw in lines[start + 1:end]:
        line = strip_comments(raw).rstrip()
        stripped = line.strip()
        if not stripped or indent(line) != block_indent + 2 or ":" not in stripped:
            continue
        key, value = stripped.split(":", 1)
        values[key.strip()] = value.strip().strip("'\"")
    return values, True


def job_blocks(lines: list[str]) -> list[tuple[str, int, int, list[str]]]:
    jobs = find_block(lines, "jobs", 0)
    if not jobs:
        return []
    _, jobs_end, jobs_indent = jobs
    result = []
    index = jobs[0] + 1
    while index < jobs_end:
        line = strip_comments(lines[index]).rstrip()
        stripped = line.strip()
        if not stripped or indent(line) != jobs_indent + 2 or ":" not in stripped:
            index += 1
            continue
        job_name = stripped.split(":", 1)[0].strip().strip("'\"")
        start = index
        end = jobs_end
        for next_index in range(index + 1, jobs_end):
            next_line = strip_comments(lines[next_index]).rstrip()
            if not next_line.strip():
                continue
            if indent(next_line) <= jobs_indent + 2:
                end = next_index
                break
        result.append((job_name, start, end, lines[start:end]))
        index = end
    return result


def has_manual_input_gate(job_text: str, input_name: str) -> bool:
    return bool(re.search(rf"github\.event\.inputs\.{input_name}\s*==\s*['\"]true['\"]", job_text))


def has_any_manual_input_gate(job_text: str, input_names: tuple[str, ...]) -> bool:
    return any(has_manual_input_gate(job_text, input_name) for input_name in input_names)


def has_release_draft_guard(job_text: str) -> bool:
    new_release_guard = "github.event.inputs.draft != 'true'" in job_text or 'github.event.inputs.draft != "true"' in job_text
    existing_release_guard = "needs.verify-existing-release.outputs.is_draft != 'true'" in job_text or 'needs.verify-existing-release.outputs.is_draft != "true"' in job_text
    return new_release_guard and existing_release_guard


def has_verified_wingetcreate_download(job_text: str) -> bool:
    return (
        "github.com/microsoft/winget-create/releases/download/" in job_text
        and "WINGETCREATE_SHA256" in job_text
        and "Get-FileHash" in job_text
        and "aka.ms/wingetcreate/latest" not in job_text
    )


def external_action_uses(job_text: str) -> list[str]:
    uses = []
    for match in re.finditer(r"(?m)^\s*uses\s*:\s*([^\s#]+)\s*(?:#.*)?$", job_text):
        uses.append(match.group(1).strip().strip("'\""))
    return uses


def action_ref_errors(text: str, path: pathlib.Path) -> list[str]:
    errors = []
    for match in MUTABLE_ACTION_REF.finditer(text):
        ref = match.group(1).strip().strip("'\"")
        if ref.startswith("./"):
            continue
        if "@" not in ref:
            errors.append(f"{path}: external action reference must include an explicit ref: uses: {ref}")
            continue
        if not FULL_COMMIT_SHA_REF.search(ref):
            errors.append(f"{path}: action reference must be pinned to a full commit SHA: uses: {ref}")
    return errors


def job_is_release_write_job(path: pathlib.Path, job_name: str, job_text: str, triggers: set[str]) -> bool:
    return (
        path.name == RELEASE_WORKFLOW
        and job_name == RELEASE_WRITE_JOB
        and "workflow_dispatch" in triggers
        and has_manual_input_gate(job_text, "publish_release")
    )


def job_is_pages_deploy_job(path: pathlib.Path, job_name: str, job_text: str, triggers: set[str]) -> bool:
    lines = job_text.splitlines()
    job_indent = indent(strip_comments(lines[0]).rstrip()) if lines else 0
    permissions, has_permissions = permissions_at_indent(lines, job_indent + 2)
    return (
        path.name == PAGES_WORKFLOW
        and job_name == "deploy"
        and "workflow_dispatch" in triggers
        and has_permissions
        and permissions == {"contents": "read", "pages": "write", "id-token": "write"}
    )


def job_is_secret_publish_job(path: pathlib.Path, job_name: str, job_text: str, triggers: set[str]) -> bool:
    if path.name != RELEASE_WORKFLOW or "workflow_dispatch" not in triggers:
        return False
    if job_name == RELEASE_WRITE_JOB:
        return has_manual_input_gate(job_text, "publish_release")
    publish_input = PUBLISH_JOB_GATES.get(job_name)
    if not publish_input:
        return False
    return has_any_manual_input_gate(job_text, EXTERNAL_PUBLISH_INTENT_INPUTS) and has_manual_input_gate(job_text, publish_input)


def has_write_permission(text: str) -> bool:
    return bool(re.search(r"(?m)^\s+[A-Za-z-]+\s*:\s*write\s*$", text)) or "write-all" in text


def validate_workflow(path: pathlib.Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    triggers = extract_on_triggers(lines)
    errors = []
    if "pull_request_target" in text:
        errors.append(f"{path}: pull_request_target is forbidden")
    permissions, has_permissions = top_level_permissions(lines)
    if not has_permissions:
        errors.append(f"{path}: missing top-level permissions; expected contents: read")
    elif permissions != {"contents": "read"}:
        errors.append(f"{path}: top-level permissions must be exactly contents: read, found {permissions}")
    if re.search(r"(?m)^\s*secrets\s*:\s*inherit\s*$", text):
        errors.append(f"{path}: broad secrets: inherit is forbidden")
    errors.extend(action_ref_errors(text, path))
    if path.name == RELEASE_WORKFLOW and "ssh-keyscan" in text and AUR_ED25519_FINGERPRINT not in text:
        errors.append(f"{path}: AUR ssh-keyscan must verify the pinned ed25519 fingerprint")
    for job_name, _, _, block_lines in job_blocks(lines):
        job_text = "\n".join(block_lines)
        release_write_job = job_is_release_write_job(path, job_name, job_text, triggers)
        pages_deploy_job = job_is_pages_deploy_job(path, job_name, job_text, triggers)
        secret_publish_job = job_is_secret_publish_job(path, job_name, job_text, triggers)
        if has_write_permission(job_text) and not release_write_job and not pages_deploy_job:
            errors.append(f"{path}: job '{job_name}' requests write permissions outside the gated create-release job")
        if "secrets." in job_text and not secret_publish_job:
            errors.append(f"{path}: job '{job_name}' uses secrets outside a gated manual release/publish job")
        publish_input = PUBLISH_JOB_GATES.get(job_name)
        if publish_input and not secret_publish_job:
            errors.append(f"{path}: job '{job_name}' must be gated by publish_release=true or publish_existing_release=true, plus {publish_input}=true")
        if secret_publish_job:
            for action_ref in external_action_uses(job_text):
                for action_name in SECRET_PUBLISH_ACTIONS_REQUIRING_SHA:
                    if action_ref.startswith(f"{action_name}@") and not FULL_COMMIT_SHA_REF.search(action_ref):
                        errors.append(f"{path}: job '{job_name}' must pin {action_name} to a full commit SHA")
            for mutable_download in MUTABLE_EXECUTABLE_DOWNLOADS:
                if mutable_download in job_text:
                    errors.append(f"{path}: job '{job_name}' downloads mutable executable URL: {mutable_download}")
        if path.name == RELEASE_WORKFLOW and job_name == "publish-winget":
            if secret_publish_job and not has_release_draft_guard(job_text):
                errors.append(f"{path}: job '{job_name}' must not publish against draft GitHub releases")
            if secret_publish_job and not has_verified_wingetcreate_download(job_text):
                errors.append(f"{path}: job '{job_name}' must download WinGetCreate from a versioned release and verify SHA256")
    return errors


def discover_workflows(root: pathlib.Path) -> list[pathlib.Path]:
    workflow_dir = root / ".github" / "workflows"
    if not workflow_dir.exists():
        return []
    return sorted(list(workflow_dir.glob("*.yml")) + list(workflow_dir.glob("*.yaml")))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate CrossMacro GitHub Actions least-privilege workflow security policy."
    )
    parser.add_argument(
        "--workflow",
        type=pathlib.Path,
        help="Validate one workflow file instead of every top-level workflow.",
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
    workflows = []
    if args.workflow:
        workflow = args.workflow if args.workflow.is_absolute() else root / args.workflow
        if not workflow.exists():
            print(f"FAIL: workflow not found: {workflow}")
            return 1
        workflows = [workflow]
    else:
        workflows = discover_workflows(root)
        if not workflows:
            print(f"FAIL: no workflows found under {root / '.github' / 'workflows'}")
            return 1
    errors = []
    for workflow in workflows:
        errors.extend(validate_workflow(workflow))
    if errors:
        print("FAIL: workflow security policy violations found")
        for error in errors:
            print(f"- {error}")
        return 1
    print("OK: workflow security policy validated")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
