namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class HostCommandProbeTests
{
    [LinuxFact]
    public async Task PkexecPreflight_RejectsNonRootOwnedSetuidExecutable()
    {
        using var fixture = new ProbeFixture();
        fixture.AddCommand("stat", "printf '65534\\n'");
        Assert.NotEqual(0, await fixture.RunPkexecProbeAsync(noNewPrivileges: false));
    }

    [LinuxFact]
    public async Task PkexecPreflight_RejectsNoNewPrivilegesEvenWhenMetadataReportsRoot()
    {
        using var fixture = new ProbeFixture();
        fixture.AddCommand("stat", "printf '0\\n'");
        Assert.NotEqual(0, await fixture.RunPkexecProbeAsync(noNewPrivileges: true));
    }

    [LinuxFact]
    public async Task PkexecPreflight_WhenMetadataReportsRootAndPrivilegesAllowed_IsUsable()
    {
        Assert.SkipUnless(File.ReadLines("/proc/self/status").Any(line => line.StartsWith("NoNewPrivs:", StringComparison.Ordinal) && line.TrimEnd().EndsWith('0')), "Inherited NoNewPrivs cannot be disabled by a child.");
        using var fixture = new ProbeFixture();
        fixture.AddCommand("stat", "printf '0\\n'");
        Assert.Equal(0, await fixture.RunPkexecProbeAsync(noNewPrivileges: false));
    }

    [LinuxFact]
    public async Task PkexecPreflight_WhenBackingMountDisablesSetuid_RejectsExecutable()
    {
        Assert.SkipUnless(File.ReadLines("/proc/self/status").Any(line => line.StartsWith("NoNewPrivs:", StringComparison.Ordinal) && line.TrimEnd().EndsWith('0')), "Inherited NoNewPrivs cannot be disabled by a child.");
        using var fixture = new ProbeFixture();
        fixture.AddCommand("stat", "printf '0\\n'");
        fixture.UsePkexecSymlink();
        fixture.AddCommand("findmnt", """
            while test "$#" -gt 1; do shift; done
            case "$1" in */backing\ file) printf 'rw,nosuid,nodev\n' ;; *) printf 'rw\n' ;; esac
            """);
        Assert.NotEqual(0, await fixture.RunPkexecProbeAsync(noNewPrivileges: false));
    }

    [LinuxFact]
    public async Task PkexecPreflight_WhenOptionalMountToolsAreMissing_RemainsUsable()
    {
        Assert.SkipUnless(File.ReadLines("/proc/self/status").Any(line => line.StartsWith("NoNewPrivs:", StringComparison.Ordinal) && line.TrimEnd().EndsWith('0')), "Inherited NoNewPrivs cannot be disabled by a child.");
        using var fixture = new ProbeFixture();
        fixture.AddCommand("stat", "printf '0\\n'");
        Assert.Equal(0, await fixture.RunPkexecProbeAsync(noNewPrivileges: false, includeSystemPath: false));
    }

    private sealed class ProbeFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"crossmacro-probe-{Guid.NewGuid():N}");

        public ProbeFixture()
        {
            Directory.CreateDirectory(_directory);
            AddCommand("pkexec", "exit 99");
            File.SetUnixFileMode(Path.Combine(_directory, "pkexec"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.SetUser);
        }

        public void AddCommand(string name, string script)
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, $"#!/bin/sh\n{script}\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        public void UsePkexecSymlink()
        {
            var path = Path.Combine(_directory, "pkexec");
            File.Move(path, Path.Combine(_directory, "backing file"));
            File.CreateSymbolicLink(path, "backing file");
        }

        public async Task<int> RunPkexecProbeAsync(bool noNewPrivileges, bool includeSystemPath = true)
        {
            // Execute the production shell program; never execute pkexec or request authorization.
            var command = (string)typeof(HostCommandProbe).GetField("PkexecUsabilityCommand", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            var startInfo = new ProcessStartInfo(noNewPrivileges ? "setpriv" : "/bin/sh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (noNewPrivileges)
            {
                startInfo.ArgumentList.Add("--no-new-privs");
                startInfo.ArgumentList.Add("/bin/sh");
            }
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
            startInfo.ArgumentList.Add("crossmacro-probe-test");
            startInfo.ArgumentList.Add(Path.Combine(_directory, "pkexec"));
            startInfo.Environment["PATH"] = includeSystemPath ? $"{_directory}:{Environment.GetEnvironmentVariable("PATH")}" : _directory;
            using var process = Process.Start(startInfo)!;
            await process.WaitForExitAsync();
            return process.ExitCode;
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
