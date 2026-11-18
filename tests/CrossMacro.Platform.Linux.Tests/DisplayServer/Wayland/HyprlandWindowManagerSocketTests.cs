namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class HyprlandWindowManagerSocketTests : IDisposable
{
    private const string Clients =
        "[{\"address\":\"0xaaa\",\"title\":\"Report [draft] (1)\",\"class\":\"editor\",\"pid\":0,\"focusHistoryID\":1}," +
        "{\"address\":\"0xbbb\",\"title\":\"Report final\",\"class\":\"viewer\",\"pid\":0,\"focusHistoryID\":0}]";

    private readonly string _runtimeDirectory = Path.Combine("/tmp", $"crossmacro-hyprland-{Guid.NewGuid():N}");
    private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(30));
    private readonly Socket _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    private readonly List<string> _commands = [];
    private readonly HyprlandIpcClient _client;
    private readonly HyprlandWindowManager _manager;
    private readonly Task _serverTask;

    public HyprlandWindowManagerSocketTests()
    {
        var instanceDirectory = Path.Combine(_runtimeDirectory, "hypr", "instance");
        _ = Directory.CreateDirectory(instanceDirectory);
        _listener.Bind(new UnixDomainSocketEndPoint(Path.Combine(instanceDirectory, ".socket.sock")));
        _listener.Listen(backlog: 4);
        _serverTask = ServeAsync(_lifetime.Token);

        _client = new HyprlandIpcClient(default(LinuxEnvironmentSnapshot) with
        {
            HyprlandInstanceSignature = "instance",
            RuntimeDir = _runtimeDirectory,
        });
        _manager = new HyprlandWindowManager(_client);
    }

    [Fact]
    public async Task FocusWindowByTitleAsync_ShouldMatchLiteralTextAndFocusFirstWindowByAddress()
    {
        Assert.True(await _manager.FocusWindowByTitleAsync("[draft] (1)", CancellationToken.None));

        Assert.Equal(["j/clients", "dispatch focuswindow address:0xaaa"], _commands);
    }

    [Theory]
    [InlineData("[.*]")]
    [InlineData("Report .*")]
    public async Task FocusWindowByTitleAsync_ShouldNotTreatTextAsRegex(string needle)
    {
        Assert.False(await _manager.FocusWindowByTitleAsync(needle, CancellationToken.None));

        Assert.Equal(["j/clients"], _commands);
    }

    [Fact]
    public async Task ClassAndCloseCommands_ShouldActOnSingleResolvedWindow()
    {
        Assert.True(await _manager.FocusWindowByClassAsync("VIEWER", CancellationToken.None));
        Assert.True(await _manager.CloseWindowByTitleAsync("Report", CancellationToken.None));

        Assert.Equal(
            ["j/clients", "dispatch focuswindow address:0xbbb", "j/clients", "dispatch closewindow address:0xaaa"],
            _commands);
    }

    [Theory]
    [InlineData("0xaaa,class:evil")]
    [InlineData("aaa\nkill")]
    [InlineData("0x")]
    [InlineData("zz")]
    public async Task AddressCommands_ShouldRejectNonHexAddresses(string address)
    {
        Assert.False(await _manager.FocusWindowByAddressAsync(address, CancellationToken.None));
        Assert.False(await _manager.CloseWindowByAddressAsync(address, CancellationToken.None));
        Assert.False(await _manager.MoveWindowToWorkspaceByAddressAsync(address, "2", CancellationToken.None));

        Assert.Empty(_commands);
    }

    [Fact]
    public async Task AddressCommands_ShouldNormalizeAddressPrefix()
    {
        Assert.True(await _manager.FocusWindowByAddressAsync("ABC", CancellationToken.None));
        Assert.True(await _manager.MoveWindowToWorkspaceByAddressAsync("0xABC", "2", CancellationToken.None));

        Assert.Equal(["dispatch focuswindow address:0xABC", "dispatch movetoworkspacesilent 2,address:0xABC"], _commands);
    }

    [Fact]
    public async Task WorkspaceCommands_ShouldRejectControlCharacters()
    {
        Assert.False(await _manager.SwitchWorkspaceAsync("1\n[[BATCH]]dispatch exit", CancellationToken.None));
        Assert.False(await _manager.MoveActiveWindowToWorkspaceAsync("1\r2", CancellationToken.None));

        Assert.Empty(_commands);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _listener.Dispose();
        try
        {
            _serverTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected: the server loop is stopped by cancelling its lifetime.
        }
        catch (SocketException)
        {
            // Expected: disposing the listener aborts the pending accept.
        }
        catch (ObjectDisposedException)
        {
            // Expected: disposing the listener aborts the pending accept.
        }

        _client.Dispose();
        _lifetime.Dispose();
        Directory.Delete(_runtimeDirectory, recursive: true);
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        while (!cancellationToken.IsCancellationRequested)
        {
            using var connection = await _listener.AcceptAsync(cancellationToken);
            var received = await connection.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
            var command = Encoding.UTF8.GetString(buffer, 0, received);
            lock (_commands)
            {
                _commands.Add(command);
            }

            var response = command.StartsWith("j/clients", StringComparison.Ordinal) ? Clients : "ok";
            _ = await connection.SendAsync(Encoding.UTF8.GetBytes(response), SocketFlags.None, cancellationToken);
            connection.Shutdown(SocketShutdown.Send);
        }
    }
}
