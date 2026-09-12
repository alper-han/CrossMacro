#!/usr/bin/env python3
"""Record build inputs, without exporting the process environment or credentials."""

import json
import os
from pathlib import Path
import platform
import re
import subprocess

sha = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
expected = os.environ["EXPECTED_SOURCE_SHA"]
if not re.fullmatch(r"[0-9a-f]{40}", expected) or sha != expected:
    raise SystemExit("Checkout does not match the requested source SHA")

metadata = {
    "source_sha": sha,
    "sdk": subprocess.check_output(["dotnet", "--version"], text=True).strip(),
    "platform": platform.platform(),
    "architecture": platform.machine(),
    "inputs": {name: os.environ.get(name, "") for name in (
        "ImageOS", "ImageVersion", "RUNNER_OS", "RUNNER_ARCH", "GITHUB_RUN_ID",
        "GITHUB_RUN_ATTEMPT", "GITHUB_WORKFLOW_REF", "GITHUB_WORKFLOW_SHA",
    )},
}
directory = Path("artifacts/build-metadata")
directory.mkdir(parents=True, exist_ok=True)
(directory / "inputs.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
if os.environ.get("GITHUB_STEP_SUMMARY"):
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as stream:
        stream.write(f"Source: `{sha}`\n\nSDK: `{metadata['sdk']}`\n")
