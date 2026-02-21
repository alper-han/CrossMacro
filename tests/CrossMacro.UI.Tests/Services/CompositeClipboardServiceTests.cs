namespace CrossMacro.UI.Tests.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;

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
        var service = new CompositeClipboardService(linux, new AvaloniaClipboardService());

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
        var service = new CompositeClipboardService(linux, new AvaloniaClipboardService());

        await service.SetTextAsync("abc");

        Assert.Single(runner.RunCalls);
        Assert.Equal(("xclip", "-selection clipboard", "abc"), runner.RunCalls[0]);
    }

    [Fact(Timeout = 5000)]
    public async Task GetTextAsync_WhenNoLinuxToolAvailable_ShouldFallbackWithoutThrow()
    {
        using var waylandScope = new EnvironmentVariableScope("WAYLAND_DISPLAY", null);
        var runner = new FakeProcessRunner();
        var linux = new LinuxShellClipboardService(runner);
        var service = new CompositeClipboardService(linux, new AvaloniaClipboardService());

        var ex = await Record.ExceptionAsync(() => service.GetTextAsync());

        Assert.Null(ex);
        Assert.True(runner.CheckCalls.Count >= 2);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Dictionary<string, bool> CheckResults { get; } = new(StringComparer.Ordinal);
        public List<string> CheckCalls { get; } = [];
        public List<(string Command, string Args, string Input)> RunCalls { get; } = [];
        public List<(string Command, string Args)> ReadCalls { get; } = [];
        public string ReadResult { get; init; } = string.Empty;

        public Task<bool> CheckCommandAsync(string command)
        {
            CheckCalls.Add(command);
            return Task.FromResult(CheckResults.TryGetValue(command, out var result) && result);
        }

        public Task RunCommandAsync(string command, string args, string input)
        {
            RunCalls.Add((command, args, input));
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string command, string[] args)
        {
            return Task.CompletedTask;
        }

        public Task<string> ReadCommandAsync(string command, string args)
        {
            ReadCalls.Add((command, args));
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
