namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastRestoreTokenStoreTests : IDisposable
{
    private readonly string _configDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-portal-restore-{Guid.NewGuid():N}");

    public PortalScreenCastRestoreTokenStoreTests()
    {
        _ = Directory.CreateDirectory(_configDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDirectory))
        {
            Directory.Delete(_configDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadRestoreTokenAsync_WhenStateBelongsToAnotherSession_ReturnsNull()
    {
        await WriteStateAsync("stale-token", restoreData: null, "crossmacro-screen-cast-v1:other-session");
        var store = CreateStore("GNOME");

        var token = await store.LoadRestoreTokenAsync(CancellationToken.None);

        Assert.Null(token);
    }

    [Fact]
    public async Task SaveRestoreStateAsync_RoundTripsTokenDataAndContext()
    {
        var environment = CreateEnvironment("HYPRLAND");
        var store = new PortalScreenCastRestoreTokenStore(_configDirectory, environment);

        await store.SaveRestoreTokenAsync("restore-token");
        await store.SaveRestoreDataAsync("restore-data");

        var reloaded = new PortalScreenCastRestoreTokenStore(_configDirectory, environment);
        Assert.Equal("restore-token", await reloaded.LoadRestoreTokenAsync(CancellationToken.None));
        Assert.Equal("restore-data", await reloaded.LoadRestoreDataAsync(CancellationToken.None));
        var state = await ReadStateAsync();
        Assert.Equal(PortalScreenCastRestoreContext.Create(environment), state.Context);
    }

    [Fact]
    public async Task LoadRestoreTokenAsync_DoesNotReadRemovedGlobalSetting()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_configDirectory, "global-settings.json"),
            """
            {"portalScreenCastRestoreToken":"legacy-token","portalScreenCastRestoreData":"legacy-data"}
            """);
        var store = CreateStore("GNOME");

        Assert.Null(await store.LoadRestoreTokenAsync(CancellationToken.None));
        Assert.Null(await store.LoadRestoreDataAsync(CancellationToken.None));
        Assert.False(File.Exists(StateFilePath));
    }

    [Fact]
    public async Task LoadRestoreTokenAsync_BindsUnscopedDedicatedStateToCurrentSession()
    {
        await WriteStateAsync("restore-token", restoreData: null, context: null);
        var environment = CreateEnvironment("GNOME");
        var store = new PortalScreenCastRestoreTokenStore(_configDirectory, environment);

        Assert.Equal("restore-token", await store.LoadRestoreTokenAsync(CancellationToken.None));
        Assert.Equal(PortalScreenCastRestoreContext.Create(environment), (await ReadStateAsync()).Context);
    }

    [Fact]
    public async Task ClearRestoreStateAsync_WritesAnEmptyDedicatedState()
    {
        var store = CreateStore("GNOME");

        await store.ClearRestoreStateAsync(CancellationToken.None);

        Assert.False(await store.HasRestoreStateAsync(CancellationToken.None));
        Assert.True(File.Exists(StateFilePath));
    }

    [Fact]
    public async Task HasRestoreStateAsync_ReturnsFalseForBlankState()
    {
        await WriteStateAsync(" ", restoreData: " ", context: null);
        var store = CreateStore("GNOME");

        Assert.False(await store.HasRestoreStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LoadRestoreTokenAsync_WhenStateFileIsMalformed_ReturnsNull()
    {
        await File.WriteAllTextAsync(StateFilePath, "not-json");
        var store = CreateStore("GNOME");

        Assert.Null(await store.LoadRestoreTokenAsync(CancellationToken.None));
        Assert.Null(await store.LoadRestoreDataAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveRestoreDataAsync_WhenContextChanges_DoesNotReuseOldToken()
    {
        var oldEnvironment = CreateEnvironment("GNOME");
        var oldStore = new PortalScreenCastRestoreTokenStore(_configDirectory, oldEnvironment);
        await oldStore.SaveRestoreTokenAsync("old-token");

        var newEnvironment = CreateEnvironment("KDE");
        var newStore = new PortalScreenCastRestoreTokenStore(_configDirectory, newEnvironment);
        await newStore.SaveRestoreDataAsync("new-data");

        Assert.Null(await newStore.LoadRestoreTokenAsync(CancellationToken.None));
        Assert.Equal("new-data", await newStore.LoadRestoreDataAsync(CancellationToken.None));
    }

    private string StateFilePath => Path.Combine(_configDirectory, "portal-screen-cast-state.json");

    private PortalScreenCastRestoreTokenStore CreateStore(string desktop) =>
        new(_configDirectory, CreateEnvironment(desktop));

    private async Task WriteStateAsync(string? restoreToken, string? restoreData, string? context)
    {
        var state = new PortalScreenCastRestoreState
        {
            RestoreToken = restoreToken,
            RestoreData = restoreData,
            Context = context,
        };
        await File.WriteAllTextAsync(
            StateFilePath,
            JsonSerializer.Serialize(state, PortalScreenCastRestoreStateJsonContext.Default.PortalScreenCastRestoreState));
    }

    private async Task<PortalScreenCastRestoreState> ReadStateAsync() =>
        JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(StateFilePath),
            PortalScreenCastRestoreStateJsonContext.Default.PortalScreenCastRestoreState)
        ?? throw new InvalidOperationException("Restore state was not persisted.");

    private static LinuxEnvironmentSnapshot CreateEnvironment(string desktop) =>
        new(
            FlatpakId: "io.github.alper_han.crossmacro",
            AppImage: null,
            SessionType: "wayland",
            WaylandDisplay: "wayland-1",
            Display: null,
            CurrentDesktop: desktop,
            GdmSession: desktop,
            HyprlandInstanceSignature: desktop is "HYPRLAND" ? "instance" : null,
            RuntimeDir: "/run/user/1000",
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null,
            CrossMacroFlatpak: null,
            FlatpakInfoExists: true,
            NiriSocket: null);
}
