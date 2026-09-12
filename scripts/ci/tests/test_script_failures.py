import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]


class ScriptFailureTests(unittest.TestCase):
    def test_work_cleanup_rejects_sources_and_input_overlap(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'repo'
            (root / 'src').mkdir(parents=True)
            marker = root / 'src' / 'keep.txt'
            marker.write_text('source')
            alias = Path(directory) / 'alias'
            alias.symlink_to(root / 'src', target_is_directory=True)
            for candidate in [root, root / 'src', root.parent, alias, root / 'artifacts' / 'input']:
                result = subprocess.run(['bash', '-c', 'source "$1"; assert_safe_linux_work_dir "$2" "$3" "$4"',
                    'guard-test', str(ROOT / 'scripts/lib/platform.sh'), str(candidate), str(root),
                    str(root / 'artifacts/input')], capture_output=True, timeout=10)
                self.assertNotEqual(result.returncode, 0, str(candidate))
                self.assertEqual(marker.read_text(), 'source')
            result = subprocess.run(['bash', '-c', 'source "$1"; assert_safe_linux_work_dir "$2" "$3" "$4"',
                'guard-test', str(ROOT / 'scripts/lib/platform.sh'), str(root / 'artifacts/work/rpm'),
                str(root), str(root / 'artifacts/input')], capture_output=True, timeout=10)
            self.assertEqual(result.returncode, 0)

    def test_cli_preserves_spaces_in_executable_and_arguments(self):
        with tempfile.TemporaryDirectory(prefix='crossmacro smoke ') as directory:
            binary = Path(directory) / 'fake cli'
            log = Path(directory) / 'calls.jsonl'
            binary.write_text('''#!/usr/bin/env python3
import json, os, sys
with open(os.environ['CALL_LOG'], 'a') as f: f.write(json.dumps(sys.argv[1:]) + '\\n')
if os.environ.get('FAIL_CLI'): sys.exit(17)
print('Usage: "status": "ok" "code": 0 "coordinateMode": "absolute" "coordinateMode": "mixed"')
''')
            binary.chmod(0o755)
            env = dict(os.environ, CALL_LOG=str(log))
            command = ['bash', str(ROOT / 'scripts/smoke/cli-smoke.sh'), '--binary', str(binary)]
            result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=15)
            self.assertEqual(result.returncode, 0, result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            self.assertEqual(len(calls), 4)
            self.assertIn('move abs 10 10', calls[2])
            env['FAIL_CLI'] = '1'
            self.assertNotEqual(subprocess.run(command, env=env, capture_output=True, timeout=15).returncode, 0)

    def test_mismatched_architecture_fails_before_publish_or_download(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(['bash', str(ROOT / 'scripts/ci/publish-linux-artifacts.sh'),
                '--rid', 'linux-x64', '--arch', 'aarch64', '--version', '1.0.0',
                '--ui-output', directory + '/ui', '--daemon-output', directory + '/daemon'],
                capture_output=True, text=True, timeout=10)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('does not match', result.stderr)
            env = dict(os.environ, TARGET_ARCH='x86_64', APPIMAGE_ARCH='aarch64')
            result = subprocess.run(['bash', str(ROOT / 'scripts/build_appimage.sh')],
                                    env=env, capture_output=True, text=True, timeout=10)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('does not match', result.stderr)

    def test_explicit_missing_daemon_never_falls_back_to_publish(self):
        with tempfile.TemporaryDirectory() as directory:
            for kind in ['deb', 'rpm']:
                env = dict(os.environ, DAEMON_DIR=directory + '/missing',
                           CROSSMACRO_ARTIFACT_ROOT=directory + '/output', TARGET_ARCH='x86_64')
                result = subprocess.run(['bash', str(ROOT / f'scripts/build_{kind}.sh')],
                                        env=env, capture_output=True, text=True, timeout=10)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('DAEMON_DIR does not contain', result.stderr)
                self.assertFalse((Path(directory) / 'output').exists())

    def test_flatpak_generator_preserves_destination_on_failed_empty_or_invalid_restore(self):
        with tempfile.TemporaryDirectory(prefix='crossmacro generator ') as directory:
            root = Path(directory)
            tools = root / 'tools'
            tools.mkdir()
            flatpak = tools / 'flatpak'
            flatpak.write_text('''#!/usr/bin/env python3
import base64, os, pathlib, sys
mode = os.environ['RESTORE_MODE']
if mode == 'failed': sys.exit(19)
assert sys.argv[-2] == "project's name.csproj", sys.argv
if mode == 'empty': sys.exit(0)
target = pathlib.Path(sys.argv[-3]) / 'example' / '1.0' / 'example.1.0.nupkg.sha512'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(base64.b64encode(b'X' * 64).decode() if mode == 'valid' else '')
''')
            flatpak.chmod(0o755)
            destination = root / 'sources.json'
            command = ['bash', str(ROOT / 'scripts/flatpak-dotnet-generator.sh'),
                       str(destination), "project's name.csproj", '-r', 'linux-x64']
            for mode in ['failed', 'empty', 'invalid', 'valid']:
                with self.subTest(mode=mode):
                    destination.write_text('original manifest')
                    env = dict(os.environ, PATH=str(tools) + os.pathsep + os.environ['PATH'], RESTORE_MODE=mode)
                    result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=15)
                    if mode == 'valid':
                        self.assertEqual(result.returncode, 0, result.stderr)
                        self.assertEqual(len(json.loads(destination.read_text())), 1)
                    else:
                        self.assertNotEqual(result.returncode, 0)
                        self.assertEqual(destination.read_text(), 'original manifest')

    def test_nix_generator_preserves_destination_on_invalid_assets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'scripts').mkdir()
            shutil.copy2(ROOT / 'scripts/update-nix-deps.sh', root / 'scripts/update-nix-deps.sh')
            tools = root / 'tools'
            tools.mkdir()
            for name in ['dotnet', 'nix-prefetch-url', 'nix-hash']:
                tool = tools / name
                tool.write_text('#!/bin/sh\nexit 0\n')
                tool.chmod(0o755)
            for project in ['CrossMacro.UI.Linux', 'CrossMacro.UI.MacOS', 'CrossMacro.Daemon']:
                obj = root / 'src' / project / 'obj'
                obj.mkdir(parents=True)
                (obj / 'project.assets.json').write_text('{ invalid JSON')
            (root / 'deps.json').write_text('original manifest')
            env = dict(os.environ, PATH=str(tools) + os.pathsep + os.environ['PATH'])
            result = subprocess.run(['bash', str(root / 'scripts/update-nix-deps.sh')], env=env,
                                    capture_output=True, timeout=15)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual((root / 'deps.json').read_text(), 'original manifest')


if __name__ == '__main__':
    unittest.main()
