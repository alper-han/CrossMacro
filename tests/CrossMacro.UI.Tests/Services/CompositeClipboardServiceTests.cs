namespace CrossMacro.UI.Tests.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Services;
using NSubstitute;

[Collection("EnvironmentVariableSensitive")]
public class CompositeClipboardServiceTests
{
    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenLinuxClipboardSupported_ShouldUseLinuxService_AndInitializeOnce()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["xclip"] = true },
            ReadResult = "linux-value"
        };
        var linux = new LinuxShellClipboardService(runner);
        var runtimeContext = new TestRuntimeContext(isFlatpak: false);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = true, ReadResult = "avalonia-value" },
            runtimeContext);

        var first = await service.GetTextAsync();
        var second = await service.GetTextAsync();

        Assert.Equal("linux-value", first);
        Assert.Equal("linux-value", second);
        Assert.Equal(1, runner.CheckCommandCallsFor("xclip"));
        Assert.Equal(2, runner.ReadCalls.Count);
    }

    [Fact(Timeout = 5000)]
    public async Task SetTextAsync_WhenLinuxClipboardSupported_ShouldUseLinuxCommandPath()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["xclip"] = true }
        };
        var linux = new LinuxShellClipboardService(runner);
        var runtimeContext = new TestRuntimeContext(isFlatpak: false);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var avalonia = new FakeClipboardService { Supported = true };
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            avalonia,
            runtimeContext);

        await service.SetTextAsync("abc");

        Assert.Empty(runner.RunCalls);
        Assert.Single(runner.WriteCalls);
        Assert.Equal(("xclip", "-selection clipboard", "abc"), runner.WriteCalls[0]);
        Assert.Empty(avalonia.Writes);
    }

    [Fact(Timeout = 5000)]
    public async Task SetTextAsync_WhenFlatpakHostClipboardSupported_ShouldUseHostBeforeSandboxTools()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true, ["xsel"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true }
        };
        var runtimeContext = new TestRuntimeContext(isFlatpak: true);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var avalonia = new FakeClipboardService { Supported = true };
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            avalonia,
            runtimeContext);

        await service.SetTextAsync("abc");

        Assert.Empty(avalonia.Writes);
        Assert.Empty(runner.RunCalls);
        Assert.Single(runner.WriteCalls);
        Assert.Equal(("flatpak-spawn", "--host wl-copy --type text/plain", "abc"), runner.WriteCalls[0]);
        Assert.DoesNotContain("xsel", runner.CheckCalls);
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenFlatpakHostClipboardReturnsNull_ShouldNotFallbackToSandboxTools()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true, ["xsel"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true },
            ReadResult = string.Empty
        };
        var runtimeContext = new TestRuntimeContext(isFlatpak: true);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = true, ReadResult = "avalonia-value" },
            runtimeContext);

        var result = await service.GetTextAsync();

        Assert.Equal(string.Empty, result);
        Assert.DoesNotContain("xsel", runner.CheckCalls);
        Assert.Single(runner.ReadCalls, call => call.Command == "flatpak-spawn" && call.Args == "--host wl-paste --no-newline");
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenHostWlPasteReportsNothingCopied_ShouldReturnEmptyWithoutSandboxFallback()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true, ["xsel"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true },
            ThrowNothingCopiedOnHostClipboardRead = true
        };
        var runtimeContext = new TestRuntimeContext(isFlatpak: true);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = true, ReadResult = "avalonia-value" },
            runtimeContext);

        var result = await service.GetTextAsync();

        Assert.Equal(string.Empty, result);
        Assert.DoesNotContain("xsel", runner.CheckCalls);
        Assert.Single(runner.ReadCalls, call => call.Command == "flatpak-spawn" && call.Args == "--host wl-paste --no-newline");
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenFlatpakHostReadFails_ShouldFallbackToSandboxShellTools()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["flatpak-spawn"] = true, ["xclip"] = true },
            HostCommandResults = { ["wl-copy"] = true, ["wl-paste"] = true },
            ReadResult = "shell-value",
            ThrowOnHostClipboardRead = true
        };
        var runtimeContext = new TestRuntimeContext(isFlatpak: true);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = true, ReadResult = "avalonia-value" },
            runtimeContext);

        var result = await service.GetTextAsync();

        Assert.Equal("shell-value", result);
        Assert.Contains(runner.ReadCalls, call => call.Command == "flatpak-spawn" && call.Args == "--host wl-paste --no-newline");
        Assert.Contains(runner.ReadCalls, call => call.Command == "xclip" && call.Args == "-selection clipboard -o");
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenFlatpakHostUnavailable_ShouldFallbackToSandboxShellTools()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["xclip"] = true },
            ReadResult = "shell-value"
        };
        var runtimeContext = new TestRuntimeContext(isFlatpak: true);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = true, ReadResult = "avalonia-value" },
            runtimeContext);

        var result = await service.GetTextAsync();

        Assert.Equal("shell-value", result);
        Assert.Single(runner.ReadCalls);
        Assert.Equal(("xclip", "-selection clipboard -o"), runner.ReadCalls[0]);
    }

    [Fact(Timeout = 5000)]
    public async Task SetTextAsync_WhenFlatpakAvaloniaUnavailable_ShouldFallbackToShellTools()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner
        {
            CheckResults = { ["xclip"] = true }
        };
        var runtimeContext = new TestRuntimeContext(isFlatpak: true);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = false },
            runtimeContext);

        await service.SetTextAsync("abc");

        Assert.Empty(runner.RunCalls);
        Assert.Single(runner.WriteCalls);
        Assert.Equal(("xclip", "-selection clipboard", "abc"), runner.WriteCalls[0]);
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenNoLinuxToolAvailable_ShouldFallbackWithoutThrow()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner();
        var runtimeContext = new TestRuntimeContext(isFlatpak: false);
        var flatpakHost = new FlatpakHostClipboardService(runner, runtimeContext);
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(
            flatpakHost,
            linux,
            new FakeClipboardService { Supported = false },
            runtimeContext);

        var ex = await Record.ExceptionAsync(() => service.GetTextAsync());

        Assert.Null(ex);
        Assert.True(runner.CheckCalls.Count >= 2);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Dictionary<string, bool> CheckResults { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> HostCommandResults { get; } = new(StringComparer.Ordinal);
        public List<string> CheckCalls { get; } = [];
        public List<(string Command, string Args, string Input)> RunCalls { get; } = [];
        public List<(string Command, string Args, string Input)> WriteCalls { get; } = [];
        public List<(string Command, string Args)> ReadCalls { get; } = [];
        public string ReadResult { get; init; } = string.Empty;
        public bool ThrowOnHostClipboardRead { get; init; }
        public bool ThrowNothingCopiedOnHostClipboardRead { get; init; }

        public Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            CheckCalls.Add(command);
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

        public Task WriteInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, args, input));
            return Task.CompletedTask;
        }

        public Task WriteInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
        {
            WriteCalls.Add((command, string.Join(' ', args), input));
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

            if (command == "flatpak-spawn" && args.Length >= 4 && args[0] == "--host" && args[1] == "sh")
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

            if (ThrowOnHostClipboardRead && command == "flatpak-spawn" && joinedArgs == "--host wl-paste --no-newline")
            {
                throw new InvalidOperationException("Simulated host clipboard read failure.");
            }

            if (ThrowNothingCopiedOnHostClipboardRead && command == "flatpak-spawn" && joinedArgs == "--host wl-paste --no-newline")
            {
                throw new InvalidOperationException("Command 'flatpak-spawn' exited with code 1: Nothing is copied");
            }

            return Task.FromResult(ReadResult);
        }

        public int CheckCommandCallsFor(string command)
        {
            var count = 0;
            foreach (var item in CheckCalls)
            {
                if (string.Equals(item, command, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool Supported { get; init; }
        public string? ReadResult { get; init; }
        public bool ThrowOnRead { get; init; }
        public List<string> Writes { get; } = [];

        public bool IsSupported => Supported;

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Writes.Add(text);
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Simulated Avalonia clipboard read failure.");
            }

            return Task.FromResult(ReadResult);
        }
    }

    private sealed class TestRuntimeContext : IRuntimeContext
    {
        public TestRuntimeContext(bool isFlatpak, string? sessionType = "wayland")
        {
            IsFlatpak = isFlatpak;
            SessionType = sessionType;
        }

        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; }
        public string? SessionType { get; }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }
}
