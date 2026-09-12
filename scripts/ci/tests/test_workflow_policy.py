import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout, redirect_stderr
import io
from unittest.mock import patch

SCRIPTS = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("policy", SCRIPTS / "workflow_policy.py")
policy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(policy)
spec = importlib.util.spec_from_file_location("runner", SCRIPTS / "run-tests.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)
SHA = "a" * 40
REPOSITORY = "owner/project"


def run(number=1, **overrides):
    result = dict(id=number, run_number=number, run_attempt=1, head_sha=SHA,
                  event="push", head_branch="dev", status="completed", conclusion="success",
                  path=".github/workflows/ci.yml", repository={"full_name": REPOSITORY},
                  head_repository={"full_name": REPOSITORY}, html_url="https://example.com/run")
    return dict(result, **overrides)


def jobs():
    return [dict(name=name, head_sha=SHA, status="completed", conclusion="success")
            for name in ("CI Quality Gate", "Release Readiness")]


class ChangeSelectionTests(unittest.TestCase):
    def test_web_and_docs_do_not_build_desktop(self):
        self.assertEqual(policy.select_changes(["website/package-lock.json", "docs/cli.md"]),
                         {"desktop": False})

    def test_docs_still_have_a_successful_gate(self):
        selection = policy.select_changes(["README.md", "docs/linux.md"])
        self.assertEqual(selection, {"desktop": False})
        needs = {name: {"result": "skipped"} for name in policy.DESKTOP_JOBS}
        needs.update({"workflow-validation": {"result": "success"},
                      "release-readiness": {"result": "skipped"}})
        policy.verify_results(needs, **selection)

    def test_website_and_bot_configuration_do_not_select_desktop(self):
        for path in ["website/src/index.astro", ".github/workflows/pages.yml", ".github/dependabot.yml"]:
            with self.subTest(path=path):
                self.assertEqual(policy.select_changes([path]), {"desktop": False})
                self.assertTrue(policy.select_changes([path, "src/Changed.cs"])["desktop"])

    def test_common_and_unknown_inputs_cannot_skip_desktop(self):
        for path in ["VERSION", "global.json", "Directory.Packages.props", "src/Foo.cs",
                     "tests/Foo.cs", "docs/man/crossmacro.1", "scripts/build_rpm.sh",
                     "flatpak/nuget-sources.json", "new-build-config"]:
            with self.subTest(path=path):
                self.assertTrue(policy.select_changes([path])["desktop"])

    def test_ci_policy_changes_validate_desktop(self):
        for path in [".github/workflows/ci.yml", "scripts/ci/workflow_policy.py"]:
            self.assertEqual(policy.select_changes([path]), {"desktop": True})

    def test_manual_and_missing_diff_are_full(self):
        self.assertTrue(all(policy.select_changes([]).values()))
        self.assertTrue(all(policy.select_changes(["docs/foo.md"], full=True).values()))

    def test_deleted_and_renamed_source_is_included(self):
        with patch.object(policy.subprocess, "check_output", return_value=b"src/deleted.cs\0docs/new.md\0") as call:
            paths = policy.changed_paths({"before": "b" * 40}, "push", SHA)
        self.assertIn("--no-renames", call.call_args.args[0])
        self.assertTrue(policy.select_changes(paths)["desktop"])

    def test_unresolvable_base_falls_back_to_full(self):
        with patch.object(policy.subprocess, "check_output", side_effect=subprocess.CalledProcessError(128, "git")):
            self.assertEqual(policy.changed_paths({"before": "b" * 40}, "push", SHA), [])
        self.assertEqual(policy.changed_paths({"before": "0" * 40}, "push", SHA), [])

    def test_pr_uses_event_base_and_tested_merge_sha(self):
        with patch.object(policy.subprocess, "check_output", return_value=b"website/foo.astro\0") as call:
            policy.changed_paths({"pull_request": {"base": {"sha": "b" * 40}}}, "pull_request", SHA)
        self.assertEqual(call.call_args.args[0][-3:], ["b" * 40, SHA, "--"])


class QualityGateTests(unittest.TestCase):
    def setUp(self):
        self.needs = {name: {"result": "success"} for name in policy.DESKTOP_JOBS}
        self.needs.update({"workflow-validation": {"result": "success"},
                           "release-readiness": {"result": "success"}})

    def test_all_selected_jobs_are_required(self):
        policy.verify_results(self.needs, True)
        for name in self.needs:
            for result in ["failure", "cancelled", "skipped", None]:
                with self.subTest(name=name, result=result):
                    needs = copy.deepcopy(self.needs)
                    needs[name]["result"] = result
                    with self.assertRaises(ValueError):
                        policy.verify_results(needs, True)

    def test_missing_job_cannot_turn_gate_green(self):
        del self.needs["package-flatpak"]
        with self.assertRaises(ValueError):
            policy.verify_results(self.needs, True)

    def test_readiness_does_not_wait_on_itself(self):
        del self.needs["release-readiness"]
        policy.verify_results(self.needs, True, readiness=True)


class ReleaseGateTests(unittest.TestCase):
    def test_success_is_exact_source_and_latest_run(self):
        self.assertEqual(policy.latest_trusted_run([run(2), run(1)], SHA, REPOSITORY)["id"], 2)
        policy.verify_ci_jobs(jobs(), SHA)

    def test_failed_or_running_latest_run_does_not_use_old_success(self):
        for overrides in [dict(conclusion="failure"), dict(status="in_progress", conclusion=None),
                          dict(conclusion="cancelled")]:
            with self.subTest(overrides=overrides), self.assertRaises(ValueError):
                policy.latest_trusted_run([run(1), run(2, **overrides)], SHA, REPOSITORY)

    def test_untrusted_or_wrong_sources_are_rejected(self):
        for overrides in [dict(head_sha="b" * 40), dict(event="pull_request"),
                          dict(head_branch="feature"), dict(path=".github/workflows/fake.yml"),
                          dict(head_repository={"full_name": "fork/project"})]:
            with self.subTest(overrides=overrides), self.assertRaises(ValueError):
                policy.latest_trusted_run([run(**overrides)], SHA, REPOSITORY)

    def test_latest_attempt_cannot_reuse_previous_attempt_success(self):
        with self.assertRaises(ValueError):
            policy.latest_trusted_run([run(), run(run_attempt=2, conclusion="failure")], SHA, REPOSITORY)

    def test_docs_only_or_duplicate_or_wrong_sha_gate_is_rejected(self):
        for entries in [jobs()[:1], jobs() + jobs()[:1],
                        [dict(j, head_sha="b" * 40) for j in jobs()],
                        [jobs()[0], dict(jobs()[1], conclusion="skipped")]]:
            with self.subTest(entries=entries), self.assertRaises(ValueError):
                policy.verify_ci_jobs(entries, SHA)

    def test_api_fetches_jobs_for_exact_latest_attempt(self):
        current = run(run_attempt=3)
        with patch.object(policy, "api", side_effect=[[current], jobs()]) as call:
            self.assertEqual(policy.verify_source_ci(REPOSITORY, SHA), current)
        self.assertIn("/attempts/3/jobs", call.call_args.args[0])

    def test_incomplete_api_response_fails_closed(self):
        with patch.object(policy, "api", return_value=[]), self.assertRaises(ValueError):
            policy.verify_source_ci(REPOSITORY, SHA)


class ReleaseStateTests(unittest.TestCase):
    def test_stable_public_release_can_publish(self):
        self.assertTrue(policy.release_state("v1.4.0", "auto", "false")["can_publish_external"])

    def test_draft_and_prerelease_cannot_publish_to_stable_channels(self):
        for tag, override, draft in [("v1.4.0", "auto", "true"), ("v1.4.0-pre.1", "auto", "false"),
                                     ("v1.4.0", "true", "false"), ("v1.4.0-pre.1", "false", "false")]:
            with self.subTest(tag=tag, override=override, draft=draft):
                self.assertFalse(policy.release_state(tag, override, draft)["can_publish_external"])

    def test_existing_release_uses_actual_state_instead_of_retry_defaults(self):
        existing = dict(tag_name="v1.4.0", draft=False, prerelease=False)
        self.assertTrue(policy.release_state("v1.4.0", "true", "true", existing)["can_publish_external"])
        for key in ["draft", "prerelease"]:
            self.assertFalse(policy.release_state("v1.4.0", "false", "false", dict(existing, **{key: True}))["can_publish_external"])

    def test_missing_or_mismatched_release_state_is_rejected(self):
        for state in [{}, dict(tag_name="v1.3.0", draft=False, prerelease=False),
                      dict(tag_name="v1.4.0", draft="false", prerelease=False)]:
            with self.assertRaises(ValueError):
                policy.release_state("v1.4.0", "auto", "false", state)


class AurGateTests(unittest.TestCase):
    def test_superseded_source_is_skipped_without_build(self):
        with patch.object(policy, "api", return_value={"object": {"sha": "b" * 40}}):
            self.assertFalse(policy.verify_aur_ci(REPOSITORY, SHA))

    def test_docs_only_ci_keeps_dev_aur_tracking(self):
        entries = [jobs()[0], dict(jobs()[1], conclusion="skipped")]
        with patch.object(policy, "api", side_effect=[{"object": {"sha": SHA}}, [run()], entries]):
            self.assertTrue(policy.verify_aur_ci(REPOSITORY, SHA))

    def test_failed_or_missing_quality_gate_never_publishes(self):
        for entries in [[], [dict(jobs()[0], conclusion="failure")],
                        [dict(jobs()[0], head_sha="b" * 40)], jobs() + jobs()[:1]]:
            with self.subTest(entries=entries), patch.object(policy, "api", side_effect=[
                    {"object": {"sha": SHA}}, [run()], entries]), self.assertRaises(ValueError):
                policy.verify_aur_ci(REPOSITORY, SHA)

    def test_new_failed_ci_attempt_never_publishes(self):
        with patch.object(policy, "api", side_effect=[{"object": {"sha": SHA}},
                [run(1), run(2, conclusion="failure")]]), self.assertRaises(ValueError):
            policy.verify_aur_ci(REPOSITORY, SHA)

    def test_desktop_ci_is_published(self):
        with patch.object(policy, "api", side_effect=[{"object": {"sha": SHA}}, [run()], jobs()]):
            self.assertTrue(policy.verify_aur_ci(REPOSITORY, SHA))


class PublicationIdentityTests(unittest.TestCase):
    def test_lightweight_and_annotated_tags_resolve_to_exact_commit(self):
        commit = {"object": {"type": "commit", "sha": SHA}}
        for responses in [[commit], [{"object": {"type": "tag", "sha": "b" * 40}}, commit]]:
            with self.subTest(responses=responses), patch.object(policy, "api", side_effect=responses):
                policy.verify_tag(REPOSITORY, "v1.4.0", SHA)

    def test_moved_tag_and_noncommit_tag_are_rejected(self):
        for obj in [{"type": "commit", "sha": "b" * 40}, {"type": "tree", "sha": SHA}]:
            with patch.object(policy, "api", return_value={"object": obj}), self.assertRaises(ValueError):
                policy.verify_tag(REPOSITORY, "v1.4.0", SHA)

    def test_numeric_version_order_ignores_drafts_and_prereleases(self):
        release = dict(tag_name="v1.10.0", draft=False, prerelease=False)
        self.assertTrue(policy.has_newer_stable_release("v1.9.0", [release]))
        self.assertFalse(policy.has_newer_stable_release("v1.10.0", [release]))
        for state in [dict(release, draft=True), dict(release, prerelease=True),
                      dict(release, tag_name="v1.10.0-rc.1")]:
            self.assertFalse(policy.has_newer_stable_release("v1.9.0", [state]))

    def test_external_publication_checks_actual_release_and_rejects_downgrade(self):
        release = dict(tag_name="v1.4.0", draft=False, prerelease=False)
        with patch.object(policy, "verify_tag"), patch.object(policy, "api", side_effect=[release, [release]]):
            policy.verify_external_release(REPOSITORY, "v1.4.0", SHA)
        with patch.object(policy, "verify_tag"), patch.object(policy, "api", side_effect=[release, [dict(release, tag_name="v1.5.0")]]), self.assertRaises(ValueError):
            policy.verify_external_release(REPOSITORY, "v1.4.0", SHA)
        with patch.object(policy, "verify_tag"), patch.object(policy, "api", return_value=dict(release, draft=True)), self.assertRaises(ValueError):
            policy.verify_external_release(REPOSITORY, "v1.4.0", SHA)

    def test_release_list_reads_every_page(self):
        response = subprocess.CompletedProcess([], 0, stdout=json.dumps([[{"tag_name": "v1.4.0"}], [{"tag_name": "v1.5.0"}]]))
        with patch.object(policy.subprocess, "run", return_value=response) as call:
            self.assertEqual(len(policy.api("repos/example/releases", "__root__")), 2)
        self.assertIn("--paginate", call.call_args.args[0])


class TestRunnerTests(unittest.TestCase):
    def test_platform_suites_and_integration_environments_are_preserved(self):
        for platform, count in [("Linux", 9), ("Windows", 8), ("MacOS", 8)]:
            with self.subTest(platform=platform):
                commands = list(runner.test_commands(platform, Path("/repo")))
                self.assertEqual(len(commands), count)
                self.assertTrue(any("CrossMacro.Mcp.Tests.csproj" in " ".join(c) for c, _ in commands))
                results = [c[c.index("--results-directory") + 1] for c, _ in commands]
                self.assertEqual(len(set(results)), count)
                for command, env in commands:
                    self.assertIn("--blame-hang-timeout", command)
                    self.assertIn("--no-build", command)
                    self.assertIn("--no-restore", command)
                    if "CrossMacro.Infrastructure.Tests.csproj" in " ".join(command):
                        self.assertEqual(env["CROSSMACRO_PROCESS_INTEGRATION_TESTS"], "1")
        linux = list(runner.test_commands("Linux", Path("/repo")))
        self.assertEqual(sum(env.get("CROSSMACRO_DBUS_INTEGRATION_TESTS") == "1" for _, env in linux), 1)
        self.assertEqual(sum(env.get("CROSSMACRO_DAEMON_INTEGRATION_TESTS") == "1" for _, env in linux), 1)

    def run_fake_suites(self, first="passed", stale=False):
        calls = []
        success = ('<TestRun xmlns="urn:test"><Results><UnitTestResult outcome="Passed" />'
                   '<UnitTestResult outcome="Passed" /><UnitTestResult outcome="NotExecuted" /></Results>'
                   '<ResultSummary outcome="Completed"><Counters total="3" executed="2" passed="2" failed="0" '
                   'notExecuted="0" /></ResultSummary></TestRun>')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            report = root / "artifacts/test-results/Linux/Core/results.trx"
            if stale:
                report.parent.mkdir(parents=True)
                report.write_text(success)

            def execute(command, *_):
                calls.append(command)
                mode = first if len(calls) == 1 else "passed"
                target = Path(command[command.index("--results-directory") + 1]) / "results.trx"
                if mode == "timeout":
                    raise subprocess.TimeoutExpired(command, 1)
                if mode != "missing":
                    content = success
                    if mode == "failed":
                        content = content.replace('passed="2" failed="0"', 'passed="1" failed="1"')
                        content = content.replace('outcome="Passed"', 'outcome="Failed"', 1)
                    elif mode == "empty":
                        content = content.replace('executed="2" passed="2"', 'executed="0" passed="0"')
                        content = content.replace('outcome="Passed"', 'outcome="NotExecuted"')
                    elif mode == "aborted":
                        content = content.replace('outcome="Completed"', 'outcome="Failed"')
                    elif mode == "malformed":
                        content = '<invalid'
                    target.write_text(content)
                return 9 if mode == "exit-failed" else 0

            with patch.object(runner, "run_bounded", side_effect=execute), \
                    patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(root / "summary.md")}), \
                    redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                code = runner.run_suites("Linux", root)
            records = json.loads((root / "artifacts/test-results/Linux/summary.json").read_text())
            self.assertIn("Core", (root / "summary.md").read_text())
            return code, records, calls

    def test_all_suites_report_counts_without_rebuilding(self):
        code, records, calls = self.run_fake_suites()
        self.assertEqual(code, 0)
        self.assertEqual(len(calls), 9)
        self.assertTrue(all(record["status"] == "passed" for record in records))
        self.assertEqual(records[0]["tests"]["notExecuted"], 1)

    def test_failed_or_hung_suite_does_not_hide_other_results(self):
        for mode in ["failed", "exit-failed", "timeout"]:
            with self.subTest(mode=mode):
                code, records, calls = self.run_fake_suites(first=mode)
                self.assertNotEqual(code, 0)
                self.assertEqual(len(calls), 9)
                self.assertEqual(records[0]["status"], "failed")
                self.assertTrue(all(record["status"] == "passed" for record in records[1:]))

    def test_zero_exit_requires_fresh_nonempty_valid_report(self):
        for mode in ["missing", "empty", "malformed", "aborted"]:
            with self.subTest(mode=mode):
                code, records, _ = self.run_fake_suites(first=mode, stale=True)
                self.assertNotEqual(code, 0)
                self.assertEqual(records[0]["status"], "failed")

    def test_every_repository_test_project_is_registered(self):
        runner.validate_project_coverage(SCRIPTS.parents[1])

    def test_new_test_project_cannot_be_silently_omitted(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for platform in ("Linux", "Windows", "MacOS"):
                for command, _ in runner.test_commands(platform, root):
                    for argument in command:
                        if argument.endswith('.csproj'):
                            path = Path(argument)
                            path.parent.mkdir(parents=True, exist_ok=True)
                            path.touch()
            runner.validate_project_coverage(root)
            (root / "tests/New.Tests.csproj").touch()
            with self.assertRaisesRegex(ValueError, 'New.Tests.csproj'):
                runner.validate_project_coverage(root)

    def test_process_exit_code_is_preserved(self):
        self.assertEqual(runner.run_bounded([sys.executable, "-c", "raise SystemExit(9)"],
                                          SCRIPTS, dict(os.environ), timeout=10), 9)

    @unittest.skipUnless(sys.platform == "linux", "Linux process-tree acceptance")
    def test_hung_suite_and_child_are_terminated(self):
        with tempfile.TemporaryDirectory() as directory:
            pid_file = Path(directory) / "child.pid"
            code = ("import subprocess, sys, time; from pathlib import Path; "
                    "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)']); "
                    "Path(sys.argv[1]).write_text(str(child.pid)); time.sleep(60)")
            with self.assertRaises(subprocess.TimeoutExpired):
                runner.run_bounded([sys.executable, "-c", code, str(pid_file)],
                                   SCRIPTS, dict(os.environ), timeout=2)
            pid = pid_file.read_text()
            # A killed child may briefly remain a zombie until adopted/reaped.
            state_file = Path(f"/proc/{pid}/stat")
            if state_file.exists():
                self.assertEqual(state_file.read_text().split()[2], "Z")

if __name__ == "__main__":
    unittest.main()
