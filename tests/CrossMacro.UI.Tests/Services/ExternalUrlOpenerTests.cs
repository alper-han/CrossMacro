using System.ComponentModel;
using System.Diagnostics;
using CrossMacro.Platform.Abstractions;
using CrossMacro.TestInfrastructure;
using CrossMacro.UI.Services;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Services;

public class ExternalUrlOpenerTests
{
    [Fact]
    public void Open_OnLinuxHost_TriesDesktopLauncherDirectly()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return ExternalUrlOpener.LaunchResult.Succeeded;
            },
            _ => true);

        opener.Open("https://github.com/alper-han/CrossMacro");

        attempts.Should().ContainSingle();
        attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "xdg-open"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.RedirectStandardOutput
            && startInfo.ArgumentList.SequenceEqual(new[] { "https://github.com/alper-han/CrossMacro" }));
    }

    [Fact]
    public void Open_OnLinux_WhenPrimaryLauncherFails_TriesOnlyExistingDesktopAgnosticFallbacks()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return attempts.Count == 2
                    ? ExternalUrlOpener.LaunchResult.Succeeded
                    : ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"{startInfo.FileName} failed"));
            },
            command => command is "xdg-open" or "gio");

        opener.Open("https://github.com/alper-han/CrossMacro");

        attempts.Should().HaveCount(2);
        attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "xdg-open"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.RedirectStandardOutput
            && startInfo.ArgumentList.SequenceEqual(new[] { "https://github.com/alper-han/CrossMacro" }));
        attempts[1].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "gio"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.RedirectStandardOutput
            && startInfo.ArgumentList.SequenceEqual(new[] { "open", "https://github.com/alper-han/CrossMacro" }));
    }

    [Fact]
    public void Open_OnLinux_WhenXdgOpenReportsPortalOpenUriFailure_DoesNotDuplicateShellPortalFailure()
    {
        const string portalError = "Error: GDBus.Error:org.freedesktop.DBus.Error.UnknownMethod: No such interface “org.freedesktop.portal.OpenURI” on object at path /org/freedesktop/portal/desktop";
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"Launcher '{startInfo.FileName}' exited with code 4: {portalError}"));
            },
            command => command == "xdg-open");

        var act = () => opener.Open("https://github.com/alper-han/CrossMacro");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        attempts.Select(startInfo => startInfo.FileName).Should().Equal("xdg-open");
        exception.ToString().Should().Contain("org.freedesktop.portal.OpenURI");
        exception.ToString().Should().NotContain("Launcher 'https://github.com/alper-han/CrossMacro' exited with code 4");
    }

    [Fact]
    public void Open_OnLinux_WhenOptionalFallbackCommandsAreMissing_DoesNotReportMissingCommandNoise()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"{startInfo.FileName} failed"));
            },
            command => command == "xdg-open");

        var act = () => opener.Open("https://github.com/alper-han/CrossMacro");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        attempts.Select(startInfo => startInfo.FileName).Should().Equal(
            "xdg-open");
        exception.ToString().Should().NotContain("gio");
        exception.ToString().Should().NotContain("sensible-browser");
    }

    [Fact]
    public void Open_OnLinux_WhenCommandDisappearsAfterProbe_DoesNotReportMissingCommandNoise()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                if (startInfo.FileName == "gio")
                {
                    throw new Win32Exception(2, "No such file or directory");
                }

                return ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException($"{startInfo.FileName} failed"));
            },
            command => command is "xdg-open" or "gio");

        var act = () => opener.Open("https://github.com/alper-han/CrossMacro");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        attempts.Select(startInfo => startInfo.FileName).Should().Equal(
            "xdg-open",
            "gio");
        exception.ToString().Should().Contain("xdg-open failed");
        exception.ToString().Should().NotContain("No such file or directory");
    }

    [Fact]
    public void Open_WhenAllAvailableLaunchersFail_ThrowsClearError()
    {
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            _ => ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException("launcher failed")),
            _ => false);

        var act = () => opener.Open("https://github.com/alper-han/CrossMacro");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to open the URL with the available desktop launchers.");
    }

    [Theory]
    [MemberData(nameof(NonLinuxRuntimeContexts))]
    public void Open_OnWindowsAndMacOS_WhenShellLauncherFails_DoesNotAttemptLinuxFallbacks(FakeRuntimeContext runtimeContext)
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            runtimeContext,
            startInfo =>
            {
                attempts.Add(startInfo);
                return ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException("shell failed"));
            },
            _ => true);

        var act = () => opener.Open("https://github.com/alper-han/CrossMacro");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to open the URL with the available desktop launchers.");
        attempts.Should().ContainSingle();
        attempts[0].FileName.Should().Be("https://github.com/alper-han/CrossMacro");
        attempts[0].UseShellExecute.Should().BeTrue();
    }

    [Theory]
    [InlineData("github.com/alper-han/CrossMacro")]
    [InlineData("file:///tmp/crossmacro")]
    [InlineData("javascript:alert(1)")]
    public void Open_WhenUrlIsNotAbsoluteHttpOrHttps_RejectsIt(string url)
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return ExternalUrlOpener.LaunchResult.Succeeded;
            },
            _ => true);

        var act = () => opener.Open(url);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Only absolute HTTP and HTTPS URLs can be opened. (Parameter 'url')");
        attempts.Should().BeEmpty();
    }

    [Fact]
    public void Open_OnFlatpak_WhenPortalLauncherFails_DoesNotAttemptHostOrDesktopFallbacks()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new ExternalUrlOpener(
            FakeRuntimeContext.Linux(isFlatpak: true),
            startInfo =>
            {
                attempts.Add(startInfo);
                return ExternalUrlOpener.LaunchResult.Failed(new InvalidOperationException("portal failed"));
            },
            _ => true);

        var act = () => opener.Open("https://github.com/alper-han/CrossMacro");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to open the URL with the available desktop launchers.");
        attempts.Should().ContainSingle();
        attempts[0].FileName.Should().Be("https://github.com/alper-han/CrossMacro");
        attempts[0].UseShellExecute.Should().BeTrue();
    }

    public static TheoryData<FakeRuntimeContext> NonLinuxRuntimeContexts()
    {
        return new TheoryData<FakeRuntimeContext>
        {
            FakeRuntimeContext.Windows(),
            FakeRuntimeContext.MacOS()
        };
    }

    public sealed class FakeRuntimeContext : IRuntimeContext
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
            return new FakeRuntimeContext(true, false, false, isFlatpak);
        }

        public static FakeRuntimeContext Windows()
        {
            return new FakeRuntimeContext(false, true, false, false);
        }

        public static FakeRuntimeContext MacOS()
        {
            return new FakeRuntimeContext(false, false, true, false);
        }
    }
}
