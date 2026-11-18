using System.Diagnostics;

namespace CrossMacro.UI.Tests.Services;

public sealed class DirectoryOpenerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"crossmacro-open-{Guid.NewGuid():N}");

    public DirectoryOpenerTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task OpenAsync_WithMissingDirectory_ThrowsWithoutLaunching()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new DirectoryOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(LaunchResult.Succeeded);
            },
            _ => true);

        var act = () => opener.OpenAsync(Path.Combine(_directory, "does-not-exist"));

        _ = await act.Should().ThrowAsync<DirectoryNotFoundException>();
        _ = attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenAsync_OnLinuxHost_UsesXdgOpenFirst()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new DirectoryOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(LaunchResult.Succeeded);
            },
            _ => true);

        await opener.OpenAsync(_directory);

        _ = attempts.Should().ContainSingle();
        _ = attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "xdg-open"
            && !startInfo.UseShellExecute
            && startInfo.RedirectStandardError
            && startInfo.ArgumentList.SequenceEqual(new[] { _directory }));
    }

    [Fact]
    public async Task OpenAsync_OnLinux_SkipsMissingLaunchersAndFallsBackToGio()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new DirectoryOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(LaunchResult.Succeeded);
            },
            command => command is "gio");

        await opener.OpenAsync(_directory);

        _ = attempts.Should().ContainSingle();
        _ = attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.FileName == "gio"
            && startInfo.ArgumentList.SequenceEqual(new[] { "open", _directory }));
    }

    [Fact]
    public async Task OpenAsync_OnLinux_WhenAllLaunchersFail_ThrowsWithAggregateFailures()
    {
        var opener = new DirectoryOpener(
            FakeRuntimeContext.Linux(),
            startInfo => throw new InvalidOperationException($"{startInfo.FileName} failed"),
            _ => true);

        var act = () => opener.OpenAsync(_directory);

        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = assertion.Which.InnerException.Should().BeOfType<AggregateException>();
    }

    [Fact]
    public async Task OpenAsync_OnLinux_WhenNoLauncherExists_ThrowsWithoutLaunching()
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new DirectoryOpener(
            FakeRuntimeContext.Linux(),
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(LaunchResult.Succeeded);
            },
            _ => false);

        var act = () => opener.OpenAsync(_directory);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No desktop launcher*");
        _ = attempts.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(NonLinuxContexts))]
    public async Task OpenAsync_OffLinuxHost_UsesShellExecute(IRuntimeContext context)
    {
        var attempts = new List<ProcessStartInfo>();
        var opener = new DirectoryOpener(
            context,
            startInfo =>
            {
                attempts.Add(startInfo);
                return Task.FromResult(LaunchResult.Succeeded);
            },
            _ => false);

        await opener.OpenAsync(_directory);

        _ = attempts.Should().ContainSingle();
        _ = attempts[0].Should().Match<ProcessStartInfo>(startInfo =>
            startInfo.UseShellExecute
            && startInfo.FileName == _directory);
    }

    [Fact]
    public async Task OpenAsync_OffLinuxHost_WhenShellFails_Throws()
    {
        var opener = new DirectoryOpener(
            FakeRuntimeContext.Windows(),
            _ => Task.FromResult(LaunchResult.Failed(new InvalidOperationException("shell failed"))),
            _ => false);

        var act = () => opener.OpenAsync(_directory);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    public static TheoryData<IRuntimeContext> NonLinuxContexts { get; } = new()
    {
        FakeRuntimeContext.Windows(),
        FakeRuntimeContext.MacOS(),
        FakeRuntimeContext.Linux(isFlatpak: true),
    };
}
