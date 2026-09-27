import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
ENABLED = os.environ.get("CROSSMACRO_RPM_PACKAGING_TESTS") == "1"
VERSION_ENV = ("VERSION", "SOURCE_TAG", "PACKAGE_VERSION_CANONICAL",
               "RPM_RELEASE_BASE", "TARGET_ARCH", "RID", "RPM_ARCH",
               "CROSSMACRO_ARTIFACT_ROOT")

@unittest.skipUnless(ENABLED, "requires the prepared Fedora RPM integration environment")
class RpmSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        missing = [p for p in ("git", "rpmbuild", "rpm", "rpm2cpio", "cpio", "tar")
                   if shutil.which(p) is None]
        if missing:
            raise RuntimeError("Missing RPM integration tools: " + ", ".join(missing))

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="crossmacro rpm ")
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.repo = self.base / "checkout"
        self.repo.mkdir()
        for relative in ("scripts/lib", "scripts/packaging/rpm", "scripts/assets",
                         "src/CrossMacro.UI/Assets/icons"):
            shutil.copytree(ROOT / relative, self.repo / relative,
                            ignore=shutil.ignore_patterns("bin", "obj", "__pycache__", ".env*"))
        for relative in ("LICENSE", "VERSION", "global.json", "Directory.Build.props",
                         "Directory.Build.targets", "Directory.Packages.props", "CrossMacro.sln",
                         "scripts/ci/publish-linux-artifacts.sh", "scripts/daemon/crossmacro.service",
                         "docs/man/crossmacro.1", "src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj",
                         "src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"):
            target = self.repo / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / relative, target)
        (self.repo / "VERSION").write_text("1.5.0\n")
        (self.repo / ".gitignore").write_text("**/obj/\n**/bin/\n**/__pycache__/\n.env*\nartifacts/\n")
        subprocess.run(["git", "init", "-q", str(self.repo)], check=True)
        self.env = {k: v for k, v in os.environ.items() if k not in VERSION_ENV}
        self.out = self.base / "packages"

    def put(self, relative, content):
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        return path

    def generate(self, overrides=None, out=None):
        return subprocess.run(
            ["bash", str(self.repo / "scripts/packaging/rpm/build-srpm.sh"),
             "--outdir", str(out or self.out), "--spec", "scripts/packaging/rpm/crossmacro.spec"],
            cwd=self.base, env=dict(self.env, **(overrides or {})),
            text=True, capture_output=True, timeout=120)

    def unpack(self, package, target):
        target.mkdir()
        payload = subprocess.run(["rpm2cpio", str(package)], check=True, capture_output=True).stdout
        subprocess.run(["cpio", "-idm", "--quiet"], input=payload, cwd=target,
                       check=True, capture_output=True)
        return target

    def test_current_tree_and_literal_metadata_survive_source_packaging(self):
        self.put("src/Probe.cs", "old bytes\n")
        self.put("src/Probe.cs", "current bytes\n")
        self.put("src/New source.cs", "new bytes\n")
        for path in ("src/obj/ignored.cs", "src/bin/output", "src/.env",
                     "scripts/ci/__pycache__/private.pyc", "private.txt",
                     "artifacts/packages/srpm/old.src.rpm"):
            self.put(path, "must not ship\n")
        for canonical, release in (("1.5.0", "1"), ("1.5.0-rc.1", "0.1.rc.1")):
            with self.subTest(canonical=canonical):
                result = self.generate({"PACKAGE_VERSION_CANONICAL": canonical})
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                package = self.out / ("crossmacro-1.5.0-" + release + ".src.rpm")
                metadata = subprocess.check_output(
                    ["rpm", "-qp", "--qf", "%{VERSION}-%{RELEASE}", str(package)], text=True)
                self.assertEqual(metadata, "1.5.0-" + release)
                extracted = self.unpack(package, self.base / ("extract-" + release))
                with tarfile.open(extracted / "crossmacro-1.5.0.tar.gz") as archive:
                    for path, data in (("src/Probe.cs", b"current bytes\n"),
                                       ("src/New source.cs", b"new bytes\n")):
                        self.assertEqual(archive.extractfile("crossmacro-1.5.0/" + path).read(), data)
                    forbidden = ("/obj/", "/bin/", "/__pycache__/", "/.env", "/artifacts/", "/private.txt")
                    self.assertFalse([name for name in archive.getnames()
                                      if any(part in name for part in forbidden)])
                rebuilt = self.base / ("prep-" + release)
                for name in ("BUILD", "BUILDROOT", "RPMS", "SOURCES", "SPECS", "SRPMS"):
                    (rebuilt / name).mkdir(parents=True)
                subprocess.run(["rpm", "-i", "--nodeps", "--define", "_topdir " + str(rebuilt),
                                str(package)], check=True, capture_output=True, env=self.env)
                hidden = self.base / "unavailable-checkout"
                self.repo.rename(hidden)
                try:
                    subprocess.run(["rpmbuild", "-bp", "--nodeps", "--define", "_topdir " + str(rebuilt),
                                    str(rebuilt / "SPECS/crossmacro.spec")], check=True,
                                   capture_output=True, env=self.env, cwd=self.base)
                finally:
                    hidden.rename(self.repo)
                self.assertTrue(any(p.read_bytes() == b"current bytes\n"
                                    for p in (rebuilt / "BUILD").rglob("Probe.cs")))

    def test_mismatched_metadata_and_overlapping_output_leave_sources_intact(self):
        sentinel = self.put("src/sentinel", "keep\n")
        for overrides, out in (({"VERSION": "9.9.9"}, self.out),
                               ({"PACKAGE_VERSION_CANONICAL": "9.9.9"}, self.out),
                               ({}, self.repo / "src/generated")):
            with self.subTest(overrides=overrides, out=out):
                self.assertNotEqual(self.generate(overrides, out).returncode, 0)
                self.assertEqual(sentinel.read_text(), "keep\n")
                self.assertFalse(list(self.out.glob("*.src.rpm")))
        (self.repo / "src/external.cs").symlink_to(self.base / "outside.cs")
        (self.base / "outside.cs").write_text("private\n")
        self.assertNotEqual(self.generate().returncode, 0)
        self.assertEqual((self.base / "outside.cs").read_text(), "private\n")
