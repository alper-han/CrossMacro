import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]


class FlatpakFlowTests(unittest.TestCase):
    def run_build(self, failure=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for relative in ["scripts/packaging/flatpak/build.sh", "scripts/lib/version.sh", "scripts/lib/platform.sh", "VERSION"]:
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(ROOT / relative, target)
            (root / "flatpak").mkdir()
            tools = root / "tools"
            tools.mkdir()
            program = """#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
with open(os.environ['CALL_LOG'], 'a') as output:
    output.write(json.dumps([Path(sys.argv[0]).name] + sys.argv[1:]) + '\\n')
if Path(sys.argv[0]).name == 'flatpak-builder' and os.environ['BUILD_FAILURE'] == 'true':
    sys.exit(19)
if Path(sys.argv[0]).name == 'flatpak' and sys.argv[1] == 'build-bundle':
    Path(sys.argv[4]).write_text('test bundle')
"""
            for name in ["flatpak", "flatpak-builder"]:
                path = tools / name
                path.write_text(program)
                path.chmod(0o755)
            log = root / "calls.jsonl"
            env = dict(os.environ, PATH=str(tools) + os.pathsep + os.environ["PATH"],
                       CALL_LOG=str(log), BUILD_FAILURE=str(failure).lower(),
                       CROSSMACRO_ARTIFACT_ROOT=str(root / "artifacts"), TARGET_ARCH="x86_64",
                       FLATPAK_ARCH="x86_64", VERSION=(ROOT / "VERSION").read_text().strip())
            result = subprocess.run(["bash", str(root / "scripts/packaging/flatpak/build.sh")],
                                    cwd=tools, env=env, capture_output=True, text=True, timeout=10)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            bundles = list((root / "artifacts/packages/flatpak").glob("*.flatpak"))
            return result, calls, len(bundles)

    def test_builds_once_exports_and_bundles_from_another_cwd(self):
        result, calls, bundle_count = self.run_build()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        builders = [call for call in calls if call[0] == "flatpak-builder"]
        self.assertEqual(len(builders), 1)
        self.assertTrue(any(arg.startswith("--repo=") for arg in builders[0]))
        self.assertEqual(calls[-1][1], "build-bundle")
        self.assertEqual(bundle_count, 1)

    def test_build_failure_never_exports_a_bundle(self):
        result, calls, bundle_count = self.run_build(failure=True)
        self.assertEqual(result.returncode, 19)
        self.assertEqual(len(calls), 1)
        self.assertEqual(bundle_count, 0)


if __name__ == "__main__":
    unittest.main()
