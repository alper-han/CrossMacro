#!/usr/bin/env python3
"""Run platform suites with bounded hangs and separate result directories."""

import os
import json
from pathlib import Path
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

COMMON = ["Core", "Application", "Cli", "Mcp", "Daemon", "Infrastructure"]


def run_bounded(command, root, env, timeout=600):
    options = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {"start_new_session": True}
    with subprocess.Popen(command, cwd=root, env=env, **options) as process:
        try:
            return process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            # The test host and its integration-test children must not outlive dotnet.
            if os.name == "nt":
                try:
                    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                   check=False, timeout=30)
                finally:
                    process.kill()
                    process.wait(timeout=10)
            else:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            process.kill()
            process.wait(timeout=10)
            raise


def test_commands(platform, root):
    for suite in COMMON + [f"Platform.{platform}", "UI"]:
        project = f"CrossMacro.{suite}.Tests"
        result_dir = root / "artifacts" / "test-results" / platform / suite
        args = ["dotnet", "test", str(root / "tests" / project / f"{project}.csproj"),
                "--configuration", "Debug", "--no-build", "--no-restore", "--verbosity", "minimal",
                "--logger", "trx;LogFileName=results.trx", "--results-directory", str(result_dir),
                "--blame-hang", "--blame-hang-timeout", "2m", "--blame-hang-dump-type", "mini"]
        env = dict(os.environ)
        if suite == "Infrastructure":
            env["CROSSMACRO_PROCESS_INTEGRATION_TESTS"] = "1"
        if suite == "Platform.Linux":
            env["CROSSMACRO_DBUS_INTEGRATION_TESTS"] = "1"
            args = ["dbus-run-session", "--"] + args
        yield args, env
        if suite == "Daemon" and platform == "Linux":
            env = dict(env, CROSSMACRO_DAEMON_INTEGRATION_TESTS="1")
            nss = args.copy()
            nss[nss.index("--results-directory") + 1] = str(result_dir / "NSS")
            yield nss + ["--filter", "FullyQualifiedName~NssUserGroupMembershipResolverTests.LibcLookup_ShouldResolveRootThroughNss"], env


def validate_project_coverage(root):
    configured = {Path(arg).resolve() for platform in ("Linux", "Windows", "MacOS")
                  for command, _ in test_commands(platform, root)
                  for arg in command if arg.endswith(".csproj")}
    actual = {path.resolve() for path in (root / "tests").glob("**/*.Tests.csproj")}
    if configured != actual:
        missing = sorted(path.name for path in actual - configured)
        stale = sorted(path.name for path in configured - actual)
        raise ValueError(f"Test project coverage mismatch; unconfigured: {missing}; missing projects: {stale}")


def read_results(path):
    document = ET.parse(path)
    counters = document.find(".//{*}ResultSummary/{*}Counters")
    if counters is None:
        raise ValueError("TRX has no test counters")
    values = {name: int(counters.attrib[name]) for name in ("total", "executed", "passed", "failed", "notExecuted")}
    # VSTest/xUnit may leave Counters.notExecuted at zero for skipped facts.
    # The individual results retain their actual NotExecuted outcomes.
    results = document.findall("./{*}Results/{*}UnitTestResult")
    values["notExecuted"] = sum(result.get("outcome") == "NotExecuted" for result in results)
    if len(results) != values["total"]:
        raise ValueError("TRX is missing individual test results")
    for counter, outcome in (("passed", "Passed"), ("failed", "Failed")):
        if values[counter] != sum(result.get("outcome") == outcome for result in results):
            raise ValueError("TRX counters disagree with individual results")
    if values["executed"] <= 0:
        raise ValueError("No tests executed")
    if any(value < 0 for value in values.values()) or values["passed"] + values["failed"] != values["executed"] or values["executed"] + values["notExecuted"] != values["total"]:
        raise ValueError("Inconsistent TRX test counters")
    if document.find(".//{*}ResultSummary").get("outcome") not in {"Completed", "Passed"} and values["failed"] == 0:
        raise ValueError("Test run did not complete successfully")
    return values


def run_suites(platform, root):
    records = []
    exit_code = 0
    for command, env in test_commands(platform, root):
        result_dir = Path(command[command.index("--results-directory") + 1])
        suite = str(result_dir.relative_to(root / "artifacts/test-results" / platform))
        result_dir.mkdir(parents=True, exist_ok=True)
        report = result_dir / "results.trx"
        report.unlink(missing_ok=True)  # A rerun must not accept an old successful TRX.
        record = {"suite": suite, "status": "failed", "tests": {}, "error": ""}
        started = time.monotonic()
        print(f"::group::Tests: {suite}", flush=True)
        try:
            returncode = run_bounded(command, root, env)
            record["tests"] = read_results(report)
            if returncode != 0 or record["tests"]["failed"]:
                exit_code = exit_code or (returncode if returncode > 0 else 1)
                record["error"] = f"Test process exit {returncode}; failed tests {record['tests']['failed']}"
            else:
                record["status"] = "passed"
        except (ValueError, KeyError, OSError, ET.ParseError, subprocess.TimeoutExpired) as error:
            record["error"] = str(error)
            exit_code = exit_code or 1
        finally:
            record["seconds"] = round(time.monotonic() - started, 2)
            records.append(record)
            print("::endgroup::", flush=True)
        if record["error"]:
            print(f"Test suite {suite} failed: {record['error']}", file=sys.stderr)

    output_dir = root / "artifacts/test-results" / platform
    (output_dir / "summary.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
    lines = [f"### Build and test results: {platform}", "", "Tests reused the Debug build; no restore or rebuild.", "",
             "| Suite | Result | Passed | Failed | Skipped | Seconds |",
             "| --- | --- | ---: | ---: | ---: | ---: |"]
    for record in records:
        counts = record["tests"]
        lines.append(f"| {record['suite']} | {record['status']} | {counts.get('passed', '—')} | "
                     f"{counts.get('failed', '—')} | {counts.get('notExecuted', '—')} | {record['seconds']} |")
    summary = "\n".join(lines) + "\n"
    print(summary)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as stream:
            stream.write(summary)
    return exit_code


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in {"Linux", "Windows", "MacOS"}:
        raise ValueError("Expected Linux, Windows or MacOS")
    root = Path(__file__).resolve().parents[2]
    validate_project_coverage(root)
    return run_suites(sys.argv[1], root)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, subprocess.TimeoutExpired) as error:
        print(f"Test execution failed: {error}", file=sys.stderr)
        sys.exit(1)
