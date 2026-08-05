namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

public sealed class ProfileTextExpansionStoreTests : IDisposable
{
    private readonly string _root;
    private readonly ProfileTextExpansionStore _store = new();

    public ProfileTextExpansionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "crossmacro-tests", nameof(ProfileTextExpansionStoreTests), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _store.Dispose();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public async Task SaveAndLoadAsync_IsolatedPerProfile()
    {
        var firstProfile = Path.Combine(_root, "first");
        var secondProfile = Path.Combine(_root, "second");
        await _store.SaveAsync(firstProfile, [new(":one", "1")]);
        await _store.SaveAsync(secondProfile, [new(":two", "2")]);

        var first = await _store.LoadAsync(firstProfile);
        var second = await _store.LoadAsync(secondProfile);

        _ = first.Should().ContainSingle().Which.Trigger.Should().Be(":one");
        _ = second.Should().ContainSingle().Which.Trigger.Should().Be(":two");
    }

    [Fact]
    public async Task ConcurrentReadModifyWrites_AreSerializedPerStore()
    {
        var profile = Path.Combine(_root, "concurrent");
        await Task.WhenAll(
            _store.SaveAsync(profile, [new(":one", "1")]),
            _store.SaveAsync(profile, [new(":two", "2")]));

        var loaded = await _store.LoadAsync(profile);

        _ = loaded.Should().ContainSingle();
        _ = loaded[0].Trigger.Should().BeOneOf(":one", ":two");
    }
}
