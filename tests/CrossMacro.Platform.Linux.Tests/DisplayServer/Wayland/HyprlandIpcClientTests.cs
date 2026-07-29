namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;


[Collection("EnvironmentVariableSensitive")]
public sealed class HyprlandIpcClientTests
{
    [Fact]
    public async Task WhenHyprlandEnvironmentMissing_ClientShouldBeUnavailableAndReturnNullResponses()
    {
        using var sigScope = new EnvironmentVariableScope("HYPRLAND_INSTANCE_SIGNATURE", value: null);
        using var runtimeScope = new EnvironmentVariableScope("XDG_RUNTIME_DIR", value: null);

        using var client = new HyprlandIpcClient();

        Assert.False(client.IsAvailable);
        Assert.Null(client.SocketPath);
        Assert.Null(await client.SendCommandAsync("cursorpos"));
        Assert.Null(await client.SendCommandAsync([]));
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
