namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;


public sealed class TextExpansionStorageServiceTests : IDisposable
{
    private readonly TextExpansionStorageService _service;
    private readonly string _testRootDirectory;

    public TextExpansionStorageServiceTests()
    {
        _testRootDirectory = Path.Combine(
            Path.GetTempPath(),
            "crossmacro-tests",
            nameof(TextExpansionStorageServiceTests),
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_testRootDirectory);
        _service = CreateService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort test cleanup tolerates expected filesystem failures.
        }
    }

    private TextExpansionStorageService CreateService()
    {
        var serviceDirectory = Path.Combine(_testRootDirectory, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(serviceDirectory);
        return new TextExpansionStorageService(serviceDirectory, new ProfileTextExpansionStore());
    }

    [Fact]
    public void FilePath_IsNotEmpty()
    {
        _ = _service.FilePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FilePath_ContainsCrossmacro()
    {
        _ = _service.FilePath.Should().Contain(_testRootDirectory);
    }

    [Fact]
    public void FilePath_EndsWithJson()
    {
        _ = _service.FilePath.Should().EndWith(".json");
    }

    [Fact]
    public void GetCurrent_Initially_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = service.GetCurrent();

        _ = result.Should().NotBeNull();
        _ = result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenFileNotExists_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.LoadAsync();

        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_NullList_ThrowsException()
    {
        var service = CreateService();

        var act = async () => await service.SaveAsync(null!);

        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_EmptyList_DoesNotThrow()
    {
        var service = CreateService();
        var emptyList = new List<TextExpansionEntry>();

        var act = async () => await service.SaveAsync(emptyList);

        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var service = CreateService();
        var expansions = new List<TextExpansionEntry>
        {
            new(":mail", "test@example.com"),
            new(
                trigger: ":sig",
                replacement: "Best regards,\nTest User",
                isEnabled: true,
                method: PasteMethod.ShiftInsert,
                insertionMode: TextInsertionMode.DirectTyping,
                directTypingMethod: DirectTypingMethod.CompatibleKeyByKey),
        };

        await service.SaveAsync(expansions);
        var loaded = await service.LoadAsync();

        _ = loaded.Should().HaveCount(2);
        _ = loaded[0].Trigger.Should().Be(":mail");
        _ = loaded[0].Replacement.Should().Be("test@example.com");
        _ = loaded[1].Trigger.Should().Be(":sig");
        _ = loaded[1].Method.Should().Be(PasteMethod.ShiftInsert);
        _ = loaded[1].InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
        _ = loaded[1].DirectTypingMethod.Should().Be(DirectTypingMethod.CompatibleKeyByKey);
    }

    [Fact]
    public async Task LoadResults_AreSnapshots_AndDoNotMutateCache()
    {
        var service = CreateService();
        await service.SaveAsync([new TextExpansionEntry(":test", "value")]);

        var asyncResult = await service.LoadAsync();
        asyncResult.Clear();
        _ = service.GetCurrent().Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAsync_WhenDirectTypingMethodIsMissing_DefaultsToFastBatch()
    {
        var service = CreateService();
        string legacyJson = "[\n  {\n    \"trigger\": \":typed\",\n    \"replacement\": \"value\",\n    \"isEnabled\": true,\n    \"method\": 0,\n    \"insertionMode\": 1\n  }\n]" + '\n';
        await File.WriteAllTextAsync(service.FilePath, legacyJson, TestContext.Current.CancellationToken);

        var loaded = await service.LoadAsync();

        _ = loaded.Should().ContainSingle();
        _ = loaded[0].DirectTypingMethod.Should().Be(DirectTypingMethod.FastBatch);
    }

    [Fact]
    public async Task LoadAsync_WhenInsertionModeIsMissing_DefaultsToPaste()
    {
        var service = CreateService();
        string legacyJson = "[\n  {\n    \"trigger\": \":mail\",\n    \"replacement\": \"test@example.com\",\n    \"isEnabled\": true,\n    \"method\": 1\n  }\n]" + '\n';

        await File.WriteAllTextAsync(service.FilePath, legacyJson, TestContext.Current.CancellationToken);

        var loaded = await service.LoadAsync();

        _ = loaded.Should().ContainSingle();
        _ = loaded[0].Trigger.Should().Be(":mail");
        _ = loaded[0].Method.Should().Be(PasteMethod.CtrlShiftV);
        _ = loaded[0].InsertionMode.Should().Be(TextInsertionMode.Paste);
    }

    [Fact]
    public async Task LoadAsync_WhenFileContainsMalformedJson_KeepsTheFileAsideAndReturnsEmptyList()
    {
        var service = CreateService();
        await service.SaveAsync(new List<TextExpansionEntry> { new(":ok", "value") });
        await File.WriteAllTextAsync(service.FilePath, "{ invalid json }", TestContext.Current.CancellationToken);

        var loaded = await service.LoadAsync();

        _ = loaded.Should().BeEmpty();
        _ = service.GetCurrent().Should().BeEmpty();
        _ = File.Exists(service.FilePath).Should().BeFalse();
        var backup = Directory.GetFiles(Path.GetDirectoryName(service.FilePath)!, "*.corrupt").Should().ContainSingle().Subject;
        _ = (await File.ReadAllTextAsync(backup, TestContext.Current.CancellationToken)).Should().Be("{ invalid json }");

        await service.SaveAsync(new List<TextExpansionEntry> { new(":new", "value") });

        _ = (await File.ReadAllTextAsync(backup, TestContext.Current.CancellationToken)).Should().Be("{ invalid json }");
    }

    [Fact]
    public async Task SaveAsync_WhenTheFileCouldNotBeRead_RefusesToOverwriteItUntilALoadSucceeds()
    {
        var service = CreateService();
        await File.WriteAllTextAsync(service.FilePath, "[{\"trigger\":\":a\",\"replacement\":\"b\"}]", TestContext.Current.CancellationToken);

        using (new FileStream(service.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            _ = (await service.LoadAsync()).Should().BeEmpty();
            _ = service.IsLoaded.Should().BeFalse();
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync([new TextExpansionEntry(":x", "y")]));
        }

        var reloaded = await service.LoadAsync();

        _ = reloaded.Should().ContainSingle();
        await service.SaveAsync(reloaded);
        _ = service.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrent_AfterSave_ReturnsSavedData()
    {
        var service = CreateService();
        var expansions = new List<TextExpansionEntry>
        {
            new(":test", "Test Value"),
        };

        await service.SaveAsync(expansions);
        var current = service.GetCurrent();

        _ = current.Should().HaveCount(1);
        _ = current[0].Trigger.Should().Be(":test");
    }

    [Fact]
    public async Task SaveAsync_WhenEnumerationThrows_PropagatesException_AndKeepsCache()
    {
        var service = CreateService();
        var baseline = new List<TextExpansionEntry> { new(":ok", "value") };
        await service.SaveAsync(baseline);

        var act = async () => await service.SaveAsync(new ThrowingExpansionEnumerable());

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("enumeration failed");

        var current = service.GetCurrent();
        _ = current.Should().HaveCount(1);
        _ = current[0].Trigger.Should().Be(":ok");
    }

    private sealed class ThrowingExpansionEnumerable : IEnumerable<TextExpansionEntry>
    {
        public IEnumerator<TextExpansionEntry> GetEnumerator() => throw new InvalidOperationException("enumeration failed");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
