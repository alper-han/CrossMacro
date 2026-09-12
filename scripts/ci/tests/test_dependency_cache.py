"""Regression coverage for cache isolation, restore inputs and compact feeds."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / 'scripts/ci/nuget-cache.py'
spec = importlib.util.spec_from_file_location('nuget_cache', SCRIPT)
cache = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cache)


class FingerprintTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.files = {
            'global.json': '{"sdk":{"version":"10.0.100"}}',
            'Directory.Packages.props': '<Project><ItemGroup><PackageVersion Include="Example" Version="1.0" /></ItemGroup></Project>',
            'src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj': '<Project><ItemGroup><ProjectReference Include="../Common/Common.csproj" /></ItemGroup></Project>',
            'src/Common/Common.csproj': '<Project><ItemGroup><PackageReference Include="Example" /></ItemGroup></Project>',
            'tests/Test.csproj': '<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>',
        }
        for name, content in self.files.items():
            p = self.root / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(content)

    def key(self, profile='publish'):
        with patch.object(cache.subprocess, 'check_output', return_value='\0'.join(self.files).encode()):
            return cache.fingerprint(self.root, profile, 'Windows')

    def test_publish_tracks_transitive_references_but_not_test_project_edits(self):
        publish, source = self.key(), self.key('source')
        p = self.root / 'tests/Test.csproj'
        p.write_text(p.read_text().replace('net10.0', 'net11.0'))
        self.assertEqual(publish, self.key())
        self.assertNotEqual(source, self.key('source'))
        p = self.root / 'src/Common/Common.csproj'
        p.write_text(p.read_text().replace('Example', 'Different'))
        self.assertNotEqual(publish, self.key())

    def test_central_versions_and_config_invalidate_cache(self):
        before = self.key()
        p = self.root / 'Directory.Packages.props'
        p.write_text(p.read_text().replace('1.0', '2.0'))
        self.assertNotEqual(before, self.key())
        before = self.key()
        self.files['NuGet.Config'] = '<configuration><packageSources><clear /></packageSources></configuration>'
        (self.root / 'NuGet.Config').write_text(self.files['NuGet.Config'])
        self.assertNotEqual(before, self.key())

    def test_comments_indentation_and_crlf_do_not_invalidate(self):
        before = self.key()
        p = self.root / 'src/Common/Common.csproj'
        p.write_bytes(p.read_text().replace('<ItemGroup>', '\r\n  <!-- explanation -->\r\n<ItemGroup>').encode())
        self.assertEqual(before, self.key())

    def test_dynamic_reference_falls_back_to_all_projects(self):
        p = self.root / 'src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj'
        p.write_text('<Project><ItemGroup><ProjectReference Include="$(SharedProject)" /></ItemGroup></Project>')
        before = self.key()
        (self.root / 'tests/Test.csproj').write_text('<Project />')
        self.assertNotEqual(before, self.key())


class CacheLifecycleTests(unittest.TestCase):
    def test_only_archives_are_saved_and_stale_versions_are_pruned(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / 'packages/example/2.0'
            package.mkdir(parents=True)
            (package / 'example.2.0.nupkg').write_bytes(b'archive')
            (package / 'example.dll').write_bytes(b'not an archive')
            feed = root / 'feed'
            feed.mkdir()
            (feed / 'example.1.0.nupkg').write_bytes(b'old')
            self.assertEqual(cache.collect(root / 'packages', feed), 1)
            self.assertEqual([p.name for p in feed.iterdir()], ['example.2.0.nupkg'])

    def test_prs_tags_and_workflow_run_never_get_write_permission(self):
        scenarios = [
            ('pull_request', 'refs/pull/1/merge', 'true', False),
            ('pull_request', 'refs/heads/dev', 'true', False),
            ('pull_request_target', 'refs/heads/dev', 'true', False),
            ('workflow_run', 'refs/heads/dev', 'true', False),
            ('workflow_dispatch', 'refs/tags/v1.0.0', 'true', False),
            ('push', 'refs/heads/feature', 'true', False),
            ('push', 'refs/heads/dev', 'false', False),
            ('push', 'refs/heads/dev', 'true', True),
            ('workflow_dispatch', 'refs/heads/main', 'true', True),
        ]
        for event, ref, writer, expected in scenarios:
            with self.subTest(event=event, ref=ref, writer=writer), tempfile.TemporaryDirectory() as temp:
                env = dict(os.environ, RUNNER_TEMP=temp, RUNNER_OS='Windows', RUNNER_ARCH='X64',
                           CACHE_PROFILE='publish', CACHE_SDK='10.0.401', CACHE_WRITER=writer,
                           GITHUB_EVENT_NAME=event, GITHUB_REF=ref,
                           GITHUB_OUTPUT=str(Path(temp) / 'output'), GITHUB_ENV=str(Path(temp) / 'env'))
                subprocess.run([sys.executable, str(SCRIPT), 'prepare'], cwd=ROOT, env=env, check=True)
                data = json.loads((Path(temp) / 'crossmacro-nuget-cache.json').read_text())
                self.assertEqual(data['writer'], expected)
                self.assertIn('RestoreAdditionalProjectSources=', (Path(temp) / 'env').read_text())

    def test_workflows_cannot_reintroduce_automatic_cache_saves(self):
        for path in (ROOT / '.github/workflows').glob('*.yml'):
            text = path.read_text()
            self.assertNotIn('uses: actions/cache@', text, str(path))
            if 'uses: actions/cache/save@' in text:
                for block in text.split('- name:')[1:]:
                    if 'uses: actions/cache/save@' in block:
                        self.assertIn("github.event_name == 'push'", block)
                        self.assertIn("github.event_name == 'workflow_dispatch'", block)
                        self.assertIn("github.ref == 'refs/heads/dev'", block)
                        self.assertIn('success()', block)

    def test_flatpak_prunes_only_obsolete_packages(self):
        spec = importlib.util.spec_from_file_location('flatpak_cache', ROOT / 'scripts/ci/flatpak-cache.py')
        flatpak = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(flatpak)
        with tempfile.TemporaryDirectory() as temp:
            downloads = Path(temp)
            for checksum in ['old', 'current']:
                directory = downloads / checksum
                directory.mkdir()
                (directory / 'example.nupkg').write_bytes(b'archive')
            sources = [dict(type='file', sha512='current', **{'dest-filename': 'example.nupkg'})]
            self.assertEqual(flatpak.prune(downloads, sources), 1)
            self.assertFalse((downloads / 'old').exists())
            self.assertTrue((downloads / 'current/example.nupkg').exists())
            self.assertEqual(flatpak.prune(downloads, sources), 0)

    def test_flatpak_downloads_are_architecture_independent(self):
        sources = json.loads((ROOT / 'flatpak/nuget-sources.json').read_text())
        for source in sources:
            self.assertNotIn('only-arches', source)
            self.assertNotIn('skip-arches', source)
        text = (ROOT / '.github/workflows/_package-flatpak.yml').read_text()
        self.assertEqual(text.count('uses: actions/cache/save@'), 1)
        for line in text.splitlines():
            if 'key: flatpak-downloads' in line:
                self.assertNotIn('runner.arch', line)

    def test_old_release_sources_can_build_without_new_cache_helpers(self):
        for name in ['_build-linux-binaries.yml', '_package-windows.yml', '_package-macos.yml']:
            text = (ROOT / '.github/workflows' / name).read_text()
            for block in text.split('- name:')[1:]:
                if 'uses: ./.github/actions/nuget-cache-restore' in block:
                    self.assertIn("!inputs.release_mode || hashFiles('.github/actions/nuget-cache-restore/action.yml') != ''", block)
                if 'uses: ./.github/actions/nuget-cache-save' in block:
                    self.assertIn("steps.nuget-cache.outcome == 'success'", block)
        text = (ROOT / '.github/workflows/_package-flatpak.yml').read_text()
        self.assertIn("!inputs.release_mode || hashFiles('scripts/ci/flatpak-cache.py') != ''", text)
        self.assertIn("steps.flatpak-downloads.outcome == 'success'", text)

    def test_website_is_only_built_and_deployed_manually(self):
        ci = (ROOT / '.github/workflows/ci.yml').read_text()
        self.assertNotIn('npm ci', ci)
        self.assertNotIn('npm run build', ci)
        pages = (ROOT / '.github/workflows/pages.yml').read_text()
        triggers = pages.split('on:\n')[1].split('\npermissions:')[0]
        self.assertEqual(triggers.strip(), 'workflow_dispatch:')
        self.assertNotIn('package-ecosystem: npm', (ROOT / '.github/dependabot.yml').read_text())


if __name__ == '__main__':
    unittest.main()
