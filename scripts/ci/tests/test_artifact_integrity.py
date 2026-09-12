import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]


class ArtifactIntegrityTests(unittest.TestCase):
    def verify(self, checksum=None, corrupt=False, extra=False, missing=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifacts = root / 'release'
            artifacts.mkdir()
            original = b'release package bytes'
            (artifacts / 'package.zip').write_bytes(b'changed bytes' if corrupt else original)
            digest = hashlib.sha256(original).hexdigest()
            if not missing:
                (artifacts / 'SHA256SUMS').write_text(
                    f'{digest}  package.zip\n' if checksum is None else checksum.replace('HASH', digest))
            if extra:
                (artifacts / 'unexpected.zip').write_bytes(original)
            manifest = root / 'manifest.json'
            manifest.write_text(json.dumps({'assets': [
                {'file': name, 'enabledByDefault': True} for name in ['package.zip', 'SHA256SUMS']]}))
            return subprocess.run(['dotnet', 'run', '--file', str(ROOT / 'scripts/ci/CrossMacroCI.cs'),
                                   '--', 'verify-artifacts', '--repo-root', str(ROOT),
                                   '--manifest', str(manifest), '--directory', str(artifacts)],
                                  capture_output=True, text=True, timeout=90)

    def test_accepts_matching_content(self):
        result = self.verify()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_rejects_changed_content(self):
        result = self.verify(corrupt=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('SHA256 mismatch', result.stdout + result.stderr)

    def test_rejects_missing_duplicate_and_unsafe_entries(self):
        for checksum in ['', 'HASH  package.zip\nHASH  package.zip\n',
                         'HASH  ../package.zip\n', 'HASH  package.zip\nHASH  unknown.zip\n',
                         'invalid hash  package.zip\n']:
            with self.subTest(checksum=checksum):
                self.assertNotEqual(self.verify(checksum=checksum).returncode, 0)

    def test_requires_manifest_and_rejects_unexpected_artifacts(self):
        self.assertNotEqual(self.verify(missing=True).returncode, 0)
        self.assertNotEqual(self.verify(extra=True).returncode, 0)


if __name__ == '__main__':
    unittest.main()
