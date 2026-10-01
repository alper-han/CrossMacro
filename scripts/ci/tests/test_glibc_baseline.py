import importlib.util
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPTS = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("glibc_baseline", SCRIPTS / "verify-glibc-baseline.py")
glibc_baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(glibc_baseline)


class GlibcVersionParsingTests(unittest.TestCase):
    def test_extracts_unique_versions_and_orders_components(self):
        output = """
        Name: GLIBC_2.2.5
        Name: GLIBC_2.35
        Name: GLIBC_2.38
        Name: GLIBC_2.35
        """

        self.assertEqual(
            glibc_baseline.extract_glibc_versions(output),
            [(2, 2, 5), (2, 35), (2, 38)],
        )

    def test_accepts_binary_at_or_below_ubuntu_22_04_baseline(self):
        self.assertTrue(glibc_baseline.is_compatible([(2, 2, 5), (2, 35)], (2, 35)))
        self.assertTrue(glibc_baseline.is_compatible([], (2, 35)))

    def test_rejects_binary_requiring_newer_glibc(self):
        self.assertFalse(glibc_baseline.is_compatible([(2, 35), (2, 38)], (2, 35)))


class GlibcFileDiscoveryTests(unittest.TestCase):
    def test_discovers_nested_elf_files_and_ignores_non_elf_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            ui = root / "usr" / "bin" / "CrossMacro.UI"
            native = root / "usr" / "lib" / "libicuuc.so.74"
            ui.parent.mkdir(parents=True)
            native.parent.mkdir(parents=True)
            shutil.copyfile(sys.executable, ui)
            shutil.copyfile(sys.executable, native)
            desktop = root / "usr" / "share" / "application.desktop"
            desktop.parent.mkdir(parents=True)
            desktop.write_text("[Desktop Entry]\n")

            self.assertEqual(
                glibc_baseline.discover_elf_files([root]),
                sorted([ui, native]),
            )

    def test_cli_accepts_a_directory_of_elf_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            binary = root / "usr" / "bin" / "CrossMacro.UI"
            binary.parent.mkdir(parents=True)
            shutil.copyfile(sys.executable, binary)

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPTS / "verify-glibc-baseline.py"),
                    "--max-glibc",
                    "999.0",
                    str(root),
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn(str(binary), result.stdout)


if __name__ == "__main__":
    unittest.main()
