import io
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]

ARCH_PAYLOAD = [
    'usr/bin/crossmacro',
    'usr/lib/crossmacro/CrossMacro.UI',
    'usr/lib/crossmacro/daemon/CrossMacro.Daemon',
    'usr/lib/systemd/system/crossmacro.service',
    'usr/lib/sysusers.d/crossmacro.conf',
    'usr/lib/modules-load.d/crossmacro.conf',
    'usr/lib/udev/rules.d/99-crossmacro.rules',
    'usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy',
    'usr/share/polkit-1/rules.d/50-crossmacro.rules',
    'usr/share/applications/CrossMacro.desktop',
    'usr/share/man/man1/crossmacro.1.gz',
    'usr/share/licenses/crossmacro/LICENSE',
]
ARCH_DEPENDENCIES = ['libice', 'libsm', 'libxi']


class ArchPackageContractTests(unittest.TestCase):
    def verify(self, omit=None, dependencies=ARCH_DEPENDENCIES):
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / 'crossmacro-1.0.0-1-x86_64.pkg.tar'
            pkginfo = ''.join(f'depend = {name}\n' for name in dependencies).encode()
            with tarfile.open(package, 'w') as archive:
                info = tarfile.TarInfo('.PKGINFO')
                info.size = len(pkginfo)
                archive.addfile(info, io.BytesIO(pkginfo))
                for path in ARCH_PAYLOAD:
                    if path != omit:
                        archive.addfile(tarfile.TarInfo(path))
            return subprocess.run(
                ['dotnet', 'run', '--file', str(ROOT / 'scripts/ci/CrossMacroCI.cs'), '--', 'verify-package',
                 '--repo-root', str(ROOT), '--package', str(package), '--kind', 'arch'],
                capture_output=True, text=True, timeout=90)

    def test_accepts_complete_payload_and_dependencies(self):
        result = self.verify()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_rejects_each_missing_payload_path(self):
        for path in ('usr/bin/crossmacro', 'usr/lib/sysusers.d/crossmacro.conf',
                     'usr/share/man/man1/crossmacro.1.gz', 'usr/share/licenses/crossmacro/LICENSE'):
            with self.subTest(path=path):
                result = self.verify(omit=path)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('missing payload path', result.stdout)

    def test_rejects_missing_runtime_dependency(self):
        result = self.verify(dependencies=['libice', 'libsm'])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("missing runtime dependency 'libxi'", result.stdout)


if __name__ == '__main__':
    unittest.main()
