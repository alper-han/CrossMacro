import importlib.util
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


if __name__ == "__main__":
    unittest.main()
