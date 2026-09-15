#!/usr/bin/env python3
"""Small, dependency-free decisions shared by CI and release workflows."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

DESKTOP_JOBS = (
    "source-validation", "build-linux-binaries", "package-linux",
    "package-flatpak", "package-windows", "package-macos",
)


def select_changes(paths, full=False):
    # Unknown inputs and an unavailable/empty diff select full validation.
    desktop = full or not paths
    for path in paths:
        # Website build/deployment is owned exclusively by manual pages.yml.
        if path.startswith("website/") or path in {".github/dependabot.yml", ".github/workflows/pages.yml"}:
            continue
        if path.startswith("docs/") and not path.startswith("docs/man/"):
            continue
        if path in {"README.md", "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md"}:
            continue
        desktop = True
    return {"desktop": desktop}


def changed_paths(event, event_name, sha):
    if event_name == "workflow_dispatch":
        return []
    base = event.get("before", "")
    if event_name == "pull_request":
        base = event.get("pull_request", {}).get("base", {}).get("sha", "")
    if not re.fullmatch(r"[0-9a-f]{40}", base) or base == "0" * 40:
        return []
    try:
        # --no-renames includes both old and new names when an input moves.
        data = subprocess.check_output(
            ["git", "diff", "--name-only", "--no-renames", "-z", base, sha, "--"],
            stderr=subprocess.DEVNULL,
        )
        return [p for p in data.decode("utf-8", errors="replace").split("\0") if p]
    except subprocess.CalledProcessError:
        return []


def verify_results(needs, desktop, readiness=False):
    expected = {"workflow-validation": True}
    expected.update({name: desktop for name in DESKTOP_JOBS})
    if not readiness:
        expected.update({"release-readiness": desktop})
    errors = []
    for name, required in expected.items():
        actual = needs.get(name, {}).get("result")
        wanted = "success" if required else "skipped"
        if actual != wanted:
            errors.append(f"{name}: expected {wanted}, received {actual or 'missing'}")
    if errors:
        raise ValueError("; ".join(errors))


def release_state(tag, prerelease, draft, existing=None):
    if not re.fullmatch(r"v\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?", tag):
        raise ValueError("Invalid release tag")
    tagged_prerelease = "-" in tag
    if existing is None:
        effective = tagged_prerelease if prerelease == "auto" else boolean(prerelease)
        is_draft = boolean(draft)
    else:
        if existing.get("tag_name") != tag:
            raise ValueError("Existing release tag does not match requested source")
        if not isinstance(existing.get("draft"), bool) or not isinstance(existing.get("prerelease"), bool):
            raise ValueError("Existing release state is incomplete")
        effective, is_draft = existing["prerelease"], existing["draft"]
    return {
        "is_draft": is_draft,
        "is_prerelease": effective,
        # A prerelease tag is never sent to stable channels, even after an override.
        "can_publish_external": not (is_draft or effective or tagged_prerelease),
    }


def boolean(value):
    if value not in {"true", "false"}:
        raise ValueError(f"Expected true or false, received {value!r}")
    return value == "true"


def api(path, collection=None):
    args = ["gh", "api", path]
    if collection:
        args += ["--paginate", "--slurp"]
    # GitHub CLI emits UTF-8 JSON.  Windows otherwise decodes it using the
    # active code page, which fails for valid non-ASCII release notes.
    result = subprocess.run(
        args,
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=120,
    )
    if result.returncode:
        # Do not copy API output or credentials to workflow logs on failure.
        raise ValueError("GitHub API request failed")
    data = json.loads(result.stdout)
    return [item for page in data for item in (page if collection == "__root__" else page[collection])] if collection else data


def verify_tag(repository, tag, sha):
    release_state(tag, "auto", "true")  # Validate input before constructing an API path.
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Source must be a full commit SHA")
    obj = api(f"repos/{repository}/git/ref/tags/{tag}")["object"]
    for _ in range(10):
        if obj["type"] == "commit":
            if obj["sha"] != sha:
                raise ValueError("Release tag moved away from the validated source SHA")
            return
        if obj["type"] != "tag":
            break
        obj = api(f"repos/{repository}/git/tags/{obj['sha']}")["object"]
    raise ValueError("Release tag does not resolve to a commit")


def has_newer_stable_release(tag, releases):
    release_state(tag, "auto", "true")
    version = tuple(map(int, tag[1:].split("-")[0].split(".")))
    for release in releases:
        candidate = release.get("tag_name", "")
        if (release.get("draft") is False and release.get("prerelease") is False
                and re.fullmatch(r"v\d+\.\d+\.\d+", candidate)
                and tuple(map(int, candidate[1:].split("."))) > version):
            return True
    return False


def verify_external_release(repository, tag, sha):
    verify_tag(repository, tag, sha)
    release = api(f"repos/{repository}/releases/tags/{tag}")
    if not release_state(tag, "auto", "false", release)["can_publish_external"]:
        raise ValueError("External publication requires a public stable release")
    releases = api(f"repos/{repository}/releases?per_page=100", "__root__")
    if has_newer_stable_release(tag, releases):
        raise ValueError("A newer stable release supersedes this external publication")


def latest_trusted_run(runs, sha, repository):
    candidates = [r for r in runs if
        r.get("head_sha") == sha
        and r.get("event") in {"push", "workflow_dispatch"}
        and r.get("head_branch") in {"dev", "main"}
        and r.get("repository", {}).get("full_name") == repository
        and r.get("head_repository", {}).get("full_name") == repository
        and r.get("path", "").split("@")[0] == ".github/workflows/ci.yml"]
    if not candidates:
        raise ValueError("No trusted CI run exists for this source SHA")
    run = max(candidates, key=lambda r: (r["run_number"], r.get("run_attempt", 1)))
    if run.get("status") != "completed" or run.get("conclusion") != "success":
        raise ValueError("The latest trusted CI run has not completed successfully")
    return run


def verify_ci_jobs(jobs, sha, required=("CI Quality Gate", "Release Readiness")):
    for name in required:
        matches = [j for j in jobs if j.get("name") == name]
        if len(matches) != 1 or any(
            j.get("head_sha") != sha or j.get("conclusion") != "success"
            or j.get("status") != "completed" for j in matches
        ):
            raise ValueError(f"Missing successful {name} for this source SHA")


def verify_source_ci(repository, sha):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Source must be a full commit SHA")
    runs = api(f"repos/{repository}/actions/workflows/ci.yml/runs?head_sha={sha}&per_page=100", "workflow_runs")
    run = latest_trusted_run(runs, sha, repository)
    # Read only the latest attempt; a previous attempt must not mask a failure.
    jobs = api(f"repos/{repository}/actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100", "jobs")
    verify_ci_jobs(jobs, sha)
    return run


def verify_aur_ci(repository, sha):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError("Source must be a full commit SHA")
    current = api(f"repos/{repository}/git/ref/heads/dev")["object"]["sha"]
    if current != sha:
        return False  # A newer dev push supersedes this publication.
    runs = api(f"repos/{repository}/actions/workflows/ci.yml/runs?head_sha={sha}&per_page=100", "workflow_runs")
    run = latest_trusted_run(runs, sha, repository)
    jobs = api(f"repos/{repository}/actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100", "jobs")
    verify_ci_jobs(jobs, sha, ("CI Quality Gate",))
    # Quality Gate verifies every selected job, including Linux builds/tests.
    # Non-desktop commits may skip Release Readiness: the git package still
    # rebuilds, installs and smoke-tests this exact source before publication.
    return True


def output(values):
    lines = [f"{key}={str(value).lower() if isinstance(value, bool) else value}" for key, value in values.items()]
    print("\n".join(lines))
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as stream:
            stream.write("\n".join(lines) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["select", "results", "release-state", "verify-source-ci", "verify-aur-ci",
                                            "verify-tag", "verify-external-release", "release-latest"])
    parser.add_argument("--readiness", action="store_true")
    args = parser.parse_args()
    if args.command == "select":
        event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
        paths = changed_paths(event, os.environ["GITHUB_EVENT_NAME"], os.environ["GITHUB_SHA"])
        output(select_changes(paths, os.environ["GITHUB_EVENT_NAME"] == "workflow_dispatch"))
    elif args.command == "results":
        verify_results(json.loads(os.environ["JOB_RESULTS"]), boolean(os.environ["DESKTOP"]), args.readiness)
        print("All selected validation jobs completed successfully")
    elif args.command == "release-state":
        existing = None
        if os.environ.get("EXISTING_RELEASE") == "true":
            existing = api(f"repos/{os.environ['GITHUB_REPOSITORY']}/releases/tags/{os.environ['SOURCE_TAG']}")
        output(release_state(os.environ["SOURCE_TAG"], os.environ.get("PRERELEASE", "auto"),
                             os.environ.get("DRAFT", "true"), existing))
    elif args.command == "verify-aur-ci":
        output({"publish": verify_aur_ci(os.environ["GITHUB_REPOSITORY"], os.environ["SOURCE_SHA"])})
    elif args.command in {"verify-tag", "verify-external-release"}:
        check = verify_tag if args.command == "verify-tag" else verify_external_release
        check(os.environ["GITHUB_REPOSITORY"], os.environ["SOURCE_TAG"], os.environ["SOURCE_SHA"])
        print("Release source and publication policy verified")
    elif args.command == "release-latest":
        releases = api(f"repos/{os.environ['GITHUB_REPOSITORY']}/releases?per_page=100", "__root__")
        output({"make_latest": not has_newer_stable_release(os.environ["SOURCE_TAG"], releases)})
    else:
        run = verify_source_ci(os.environ["GITHUB_REPOSITORY"], os.environ["SOURCE_SHA"])
        output({"ci_run_id": run["id"], "ci_run_url": run["html_url"]})


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, subprocess.SubprocessError) as error:
        print(f"CI policy failed: {error}", file=sys.stderr)
        sys.exit(1)
