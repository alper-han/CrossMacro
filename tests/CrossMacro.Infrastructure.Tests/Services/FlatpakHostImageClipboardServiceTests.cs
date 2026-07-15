namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Linux.Clipboard;
using FlatpakHostImageClipboardService = CrossMacro.Platform.Linux.Clipboard.FlatpakHostImageClipboardService;
using CrossMacro.Platform.Abstractions;

public sealed class FlatpakHostImageClipboardServiceTests
{
    [Fact]
    public async Task SetPngAsync_WhenHostWaylandToolAvailable_UsesFlatpakSpawnWlCopy()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["wl-copy"] = true },
        };
        var service = new FlatpakHostImageClipboardService(runner, new TestRuntimeContext("wayland"));
        byte[] png = [1, 2, 3];

        await service.SetPngAsync(png);

        Assert.Single(runner.WriteCalls);
        Assert.Equal("flatpak-spawn", runner.WriteCalls[0].Command);
        Assert.Equal("--host wl-copy --type image/png", runner.WriteCalls[0].Args);
        Assert.Equal("010203", runner.WriteCalls[0].InputHex);
    }

    [Fact]
    public async Task SetPngAsync_WhenHostXclipAvailable_UsesFlatpakSpawnXclip()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
            HostCommandResults = { ["xclip"] = true },
        };
        var service = new FlatpakHostImageClipboardService(runner, new TestRuntimeContext("x11"));
        byte[] png = [1, 2, 3];

        await service.SetPngAsync(png);

        Assert.Single(runner.WriteCalls);
        Assert.Equal("flatpak-spawn", runner.WriteCalls[0].Command);
        Assert.Equal("--host xclip -selection clipboard -t image/png -i", runner.WriteCalls[0].Args);
    }

    [Fact]
    public async Task SetPngAsync_WhenFlatpakSpawnMissing_ThrowsUnavailable()
    {
        var runner = new FakeProcessRunner();
        var service = new FlatpakHostImageClipboardService(runner, new TestRuntimeContext("wayland"));
        byte[] png = [1, 2, 3];

        await Assert.ThrowsAsync<ImageClipboardUnavailableException>(() => service.SetPngAsync(png));
        Assert.False(service.IsSupported);
    }

    [Fact]
    public async Task SetPngAsync_WhenNoHostImageClipboardToolAvailable_ThrowsUnavailable()
    {
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true },
        };
        var service = new FlatpakHostImageClipboardService(runner, new TestRuntimeContext("wayland"));
        byte[] png = [1, 2, 3];

        await Assert.ThrowsAsync<ImageClipboardUnavailableException>(() => service.SetPngAsync(png));
        Assert.False(service.IsSupported);
    }

    private sealed class FakeProcessRunner : CrossMacro.Infrastructure.Services.IProcessRunner
    {
        public Dictionary<string, bool> CheckResults { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> HostCommandResults { get; } = new(StringComparer.Ordinal);
        public List<(string Command, string Args, string InputHex)> WriteCalls { get; } = [];
        public List<(string Command, string Args)> ReadCalls { get; } = [];

        public Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CheckResults.TryGetValue(command, out var result) && result);
        }

        public Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task WriteClipboardInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, args, Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(input))));
            return Task.CompletedTask;
        }

        public Task WriteClipboardInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, string.Join(' ', args), Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(input))));
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
            return Task.FromResult(string.Empty);
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
            }

            return Task.FromResult(string.Empty);
        }
    }

    private sealed class TestRuntimeContext : IRuntimeContext
    {
        public TestRuntimeContext(string? sessionType)
        {
            SessionType = sessionType;
        }

        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak => true;
        public string? SessionType { get; }
    }
}
