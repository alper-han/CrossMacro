
namespace CrossMacro.UI.Tests.Services;

public sealed class ExternalUrlOpenerTests
{
    private static readonly Uri RepositoryUri = new("https://github.com/alper-han/CrossMacro", UriKind.Absolute);
    private static readonly string[] XdgOpenArguments = ["https://github.com/alper-han/CrossMacro"];
    private static readonly string[] GioOpenArguments = ["open", "https://github.com/alper-han/CrossMacro"];

    [Fact]
    public async Task Open_OnLinuxHost_TriesDesktopLauncherDirectly()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(ExternalUrlOpener.LaunchResult.Succeeded);
            },
            _ => true);

        await opener.OpenAsync(RepositoryUri);

        _ = attempts.Should().ContainSingle();
        _ = attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "xdg-open"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.RedirectStandardOutput
            && startInfo.ArgumentList.SequenceEqual(XdgOpenArguments));
    }

    [Fact]
    public async Task Open_OnLinux_WhenPrimaryLauncherFails_TriesOnlyExistingDesktopAgnosticFallbacks()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(attempts.Count is 2
                    ? ExternalUrlOpener.LaunchResult.Succeeded
                    : ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"{startInfo.FileName} failed")));
            },
            command => command is "xdg-open" or "gio");

        await opener.OpenAsync(RepositoryUri);

        _ = attempts.Should().HaveCount(2);
        _ = attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "xdg-open"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.RedirectStandardOutput
            && startInfo.ArgumentList.SequenceEqual(XdgOpenArguments));
        _ = attempts[1].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "gio"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.RedirectStandardOutput
            && startInfo.ArgumentList.SequenceEqual(GioOpenArguments));
    }

    [Fact]
    public async Task Open_OnLinux_WhenXdgOpenReportsPortalOpenUriFailure_DoesNotDuplicateShellPortalFailure()
    {
        const string portalError = "Error: GDBus.Error:org.freedesktop.DBus.Error.UnknownMethod: No such interface “org.freedesktop.portal.OpenURI” on object at path /org/freedesktop/portal/desktop";
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"Launcher '{startInfo.FileName}' exited with code 4: {portalError}")));
            },
            command => command is "xdg-open");

        var act = async () => await opener.OpenAsync(RepositoryUri);

        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        _ = attempts.Select(startInfo => startInfo.FileName).Should().Equal("xdg-open");
        _ = exception.ToString().Should().Contain("org.freedesktop.portal.OpenURI");
        _ = exception.ToString().Should().NotContain("Launcher 'https://github.com/alper-han/CrossMacro' exited with code 4");
    }

    [Fact]
    public async Task Open_OnLinux_WhenOptionalFallbackCommandsAreMissing_DoesNotReportMissingCommandNoise()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"{startInfo.FileName} failed")));
            },
            command => command is "xdg-open");

        var act = async () => await opener.OpenAsync(RepositoryUri);

        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        _ = attempts.Select(startInfo => startInfo.FileName).Should().Equal(
            "xdg-open");
        _ = exception.ToString().Should().NotContain("gio");
        _ = exception.ToString().Should().NotContain("sensible-browser");
    }

    [Fact]
    public async Task Open_OnLinux_WhenCommandDisappearsAfterProbe_DoesNotReportMissingCommandNoise()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                if (startInfo.FileName is "gio")
                {
                    throw new Win32Exception(2, "No such file or directory");
                }

                return Task.FromResult(ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"{startInfo.FileName} failed")));
            },
            command => command is "xdg-open" or "gio");

        var act = async () => await opener.OpenAsync(RepositoryUri);

        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        _ = attempts.Select(startInfo => startInfo.FileName).Should().Equal(
            "xdg-open",
            "gio");
        _ = exception.ToString().Should().Contain("xdg-open failed");
        _ = exception.ToString().Should().NotContain("No such file or directory");
    }

    [Fact]
    public async Task Open_WhenAllAvailableLaunchersFail_ThrowsClearError()
    {
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            _ => Task.FromResult(ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException("launcher failed"))),
            _ => false);

        var act = async () => await opener.OpenAsync(RepositoryUri);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to open the URL with the available desktop launchers.");
    }

    [Theory]
    [MemberData(nameof(NonLinuxRuntimeContexts))]
    public async Task Open_OnWindowsAndMacOS_WhenShellLauncherFails_DoesNotAttemptLinuxFallbacks(IRuntimeContext runtimeContext)
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            runtimeContext,
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException("shell failed")));
            },
            _ => true);

        var act = async () => await opener.OpenAsync(RepositoryUri);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to open the URL with the available desktop launchers.");
        _ = attempts.Should().ContainSingle();
        _ = attempts[0].FileName.Should().Be("https://github.com/alper-han/CrossMacro");
        _ = attempts[0].UseShellExecute.Should().BeTrue();
    }

    [Theory]
    [InlineData("github.com/alper-han/CrossMacro")]
    [InlineData("file:///tmp/crossmacro")]
    [InlineData("javascript:alert(1)")]
    public async Task Open_WhenUrlIsNotAbsoluteHttpOrHttps_RejectsIt(string value)
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(ExternalUrlOpener.LaunchResult.Succeeded);
            },
            _ => true);

        Func<string, Task> open = opener.OpenAsync;
        var act = async () => await open(value);

        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Only absolute HTTP and HTTPS URLs can be opened. (Parameter 'url')");
        _ = attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Open_OnFlatpak_WhenPortalLauncherFails_DoesNotAttemptHostOrDesktopFallbacks()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(isFlatpak: true),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException("portal failed")));
            },
            _ => true);

        var act = async () => await opener.OpenAsync(RepositoryUri);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to open the URL with the available desktop launchers.");
        _ = attempts.Should().ContainSingle();
        _ = attempts[0].FileName.Should().Be("https://github.com/alper-han/CrossMacro");
        _ = attempts[0].UseShellExecute.Should().BeTrue();
    }

    public static TheoryData<IRuntimeContext> NonLinuxRuntimeContexts()
    {
        return new TheoryData<IRuntimeContext>
        {
            FakeRuntimeContext.Windows(),
            FakeRuntimeContext.MacOS(),
        };
    }

    internal sealed class FakeRuntimeContext : IRuntimeContext
    {
        private FakeRuntimeContext(bool isLinux, bool isWindows, bool isMacOS, bool isFlatpak)
        {
            IsLinux = isLinux;
            IsWindows = isWindows;
            IsMacOS = isMacOS;
            IsFlatpak = isFlatpak;
        }

        public bool IsLinux { get; }
        public bool IsWindows { get; }
        public bool IsMacOS { get; }
        public bool IsFlatpak { get; }
        public string? SessionType => null;

        public static FakeRuntimeContext Linux(bool isFlatpak = false)
        {
            return new FakeRuntimeContext(isLinux: true, isWindows: false, isMacOS: false, isFlatpak);
        }

        public static FakeRuntimeContext Windows()
        {
            return new FakeRuntimeContext(isLinux: false, isWindows: true, isMacOS: false, isFlatpak: false);
        }

        public static FakeRuntimeContext MacOS()
        {
            return new FakeRuntimeContext(isLinux: false, isWindows: false, isMacOS: true, isFlatpak: false);
        }
    }
}
