namespace CrossMacro.Infrastructure.Tests.Services;


[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class FlatpakHostClipboardServiceTests
{
    [Fact]
    public async Task SetTextAsync_WhenHostWaylandToolsAvailable_UsesFlatpakSpawnWlCopy()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true },
        };
        var service = new FlatpakHostClipboardService(runner, new TestRuntimeContext("wayland"));

        await service.SetTextAsync("hello", CancellationToken.None);

        Assert.Empty(runner.RunCalls);
        _ = Assert.Single(runner.WriteCalls);
        Assert.Equal("flatpak-spawn", runner.WriteCalls[0].Command);
        Assert.Equal("--host wl-copy --type text/plain", runner.WriteCalls[0].Args);
        Assert.Equal("hello", runner.WriteCalls[0].Input);
    }

    [Fact]
    public async Task GetTextAsync_WhenHostWaylandToolsAvailable_UsesFlatpakSpawnWlPaste()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true },
            ReadResult = "host-value",
        };
        var service = new FlatpakHostClipboardService(runner, new TestRuntimeContext("wayland"));

        var result = await service.GetTextAsync(CancellationToken.None);

        Assert.Equal("host-value", result);
        Assert.Contains(runner.ReadCalls, call =>
            call.Command is "flatpak-spawn" && call.Args is "--host wl-paste --no-newline");
    }

    [Fact]
    public async Task SetTextAsync_WhenWaylandUnavailableAndXclipAvailable_UsesHostXclip()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["xclip"] = true },
        };
        var service = new FlatpakHostClipboardService(runner, new TestRuntimeContext("x11"));

        await service.SetTextAsync("hello", CancellationToken.None);

        Assert.Empty(runner.RunCalls);
        _ = Assert.Single(runner.WriteCalls);
        Assert.Equal("--host xclip -selection clipboard", runner.WriteCalls[0].Args);
    }

    [Fact]
    public async Task SetTextAsync_WhenX11SessionAndWaylandToolsAvailable_UsesHostXclip()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true, ["xclip"] = true },
        };
        var service = new FlatpakHostClipboardService(runner, new TestRuntimeContext("x11"));

        await service.SetTextAsync("hello", CancellationToken.None);

        Assert.Empty(runner.RunCalls);
        _ = Assert.Single(runner.WriteCalls);
        Assert.Equal("--host xclip -selection clipboard", runner.WriteCalls[0].Args);
    }

    [Fact]
    public async Task SetTextAsync_WhenSessionUnknownButWaylandDisplayExists_DoesNotUseHostXclip()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["xclip"] = true },
        };
        var service = new FlatpakHostClipboardService(
            runner,
            new TestRuntimeContext(sessionType: null),
                name => string.Equals(name, "WAYLAND_DISPLAY", StringComparison.Ordinal) ? "wayland-1" : null);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetTextAsync("hello", CancellationToken.None));

        Assert.Empty(runner.RunCalls);
        Assert.DoesNotContain(runner.ReadCalls, call => call.Args.Contains("command -v xclip", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetTextAsync_WhenNoHostClipboardToolAvailable_Throws()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
        };
        var service = new FlatpakHostClipboardService(runner, new TestRuntimeContext("wayland"));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetTextAsync("hello", CancellationToken.None));
        Assert.False(service.IsSupported);
    }

    [Fact]
    public async Task InitializeAsync_WhenFlatpakSpawnMissing_IsUnsupported()
    {
        var runner = new FakeProcessRunner();
        var service = new FlatpakHostClipboardService(runner, new TestRuntimeContext("wayland"));

        await service.InitializeAsync(CancellationToken.None);

        Assert.False(service.IsSupported);
    }

    private sealed class FakeProcessRunner : CrossMacro.Platform.Abstractions.IProcessRunner
    {
        public Dictionary<string, bool> CheckResults { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> HostCommandResults { get; } = new(StringComparer.Ordinal);
        public List<(string Command, string Args, string Input)> RunCalls { get; } = [];
        public List<(string Command, string Args, string Input)> WriteCalls { get; } = [];
        public List<(string Command, string Args)> ReadCalls { get; } = [];
        public string ReadResult { get; init; } = string.Empty;

        public Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CheckResults.TryGetValue(command, out var result) && result);
        }

        public Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default)
        {
            RunCalls.Add((command, args, input));
            return Task.CompletedTask;
        }

        public Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
        {
            RunCalls.Add((command, string.Join(' ', args), input));
            return Task.CompletedTask;
        }

        public Task WriteClipboardInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, args, input));
            return Task.CompletedTask;
        }

        public Task WriteClipboardInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, string.Join(' ', args), input));
            return Task.CompletedTask;
        }

        public Task WriteClipboardInputAndCloseAsync(string command, string[] args, ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, string.Join(' ', args), Convert.ToHexString(input.Span)));
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string command, string[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> ReadCommandAsync(string command, string args, CancellationToken cancellationToken = default)
        {
            ReadCalls.Add((command, args));
            return Task.FromResult(ReadResult);
        }

        public Task<string> ReadCommandAsync(string command, string[] args, CancellationToken cancellationToken = default)
        {
            var joinedArgs = string.Join(' ', args);
            ReadCalls.Add((command, joinedArgs));

            if (command is "flatpak-spawn" && args.Length >= 4 && args[0] is "--host" && args[1] is "sh")
            {
                foreach (var item in HostCommandResults)
                {
                    if (joinedArgs.Contains($"command -v {item.Key}", StringComparison.Ordinal))
                    {
                        return Task.FromResult(item.Value ? "yes" : string.Empty);
                    }
                }

                return Task.FromResult(string.Empty);
            }

            return Task.FromResult(ReadResult);
        }
    }

    private sealed class TestRuntimeContext(string? sessionType) : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak => true;
        public string? SessionType { get; } = sessionType;
    }
}
