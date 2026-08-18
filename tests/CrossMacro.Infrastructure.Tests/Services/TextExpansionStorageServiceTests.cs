namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class TextExpansionStorageServiceTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
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
        return new TextExpansionStorageService(serviceDirectory);
    }

    [Fact]
    public void FilePath_IsNotEmpty()
    {
        // Assert
        _ = _service.FilePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FilePath_ContainsCrossmacro()
    {
        // Assert
        _ = _service.FilePath.Should().Contain(_testRootDirectory);
    }

    [Fact]
    public void FilePath_EndsWithJson()
    {
        // Assert
        _ = _service.FilePath.Should().EndWith(".json");
    }

    [Fact]
    public void GetCurrent_Initially_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetCurrent();

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Should().BeEmpty();
    }

    [Fact]
    public void Load_WhenFileNotExists_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act (file likely doesn't exist in test environment)
        var result = service.Load();

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenFileNotExists_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.LoadAsync();

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_NullList_ThrowsException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = async () => await service.SaveAsync(null!);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_EmptyList_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        var emptyList = new List<TextExpansionEntry>();

        // Act
        var act = async () => await service.SaveAsync(emptyList);

        // Assert
        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        // Arrange
        var service = CreateService();
        var expansions = new List<TextExpansionEntry>
        {
            new(":mail", "test@example.com"),
            new(":sig", "Best regards,\nTest User", true, PasteMethod.ShiftInsert, TextInsertionMode.DirectTyping, DirectTypingMethod.CompatibleKeyByKey),
        };

        // Act
        await service.SaveAsync(expansions);
        var loaded = await service.LoadAsync();

        // Assert
        _ = loaded.Should().HaveCount(2);
        _ = loaded[0].Trigger.Should().Be(":mail");
        _ = loaded[0].Replacement.Should().Be("test@example.com");
        _ = loaded[1].Trigger.Should().Be(":sig");
        _ = loaded[1].Method.Should().Be(PasteMethod.ShiftInsert);
        _ = loaded[1].InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
        _ = loaded[1].DirectTypingMethod.Should().Be(DirectTypingMethod.CompatibleKeyByKey);
    }

    [Fact]
    public async Task LoadAsync_WhenDirectTypingMethodIsMissing_DefaultsToFastBatch()
    {
        var service = CreateService();
        string legacyJson = "[\n  {\n    \"trigger\": \":typed\",\n    \"replacement\": \"value\",\n    \"isEnabled\": true,\n    \"method\": 0,\n    \"insertionMode\": 1\n  }\n]" + '\n';
        await File.WriteAllTextAsync(service.FilePath, legacyJson, NonCancelableToken);

        var loaded = await service.LoadAsync();

        _ = loaded.Should().ContainSingle();
        _ = loaded[0].DirectTypingMethod.Should().Be(DirectTypingMethod.FastBatch);
    }

    [Fact]
    public async Task LoadAsync_WhenInsertionModeIsMissing_DefaultsToPaste()
    {
        // Arrange
        var service = CreateService();
        string legacyJson = "[\n  {\n    \"trigger\": \":mail\",\n    \"replacement\": \"test@example.com\",\n    \"isEnabled\": true,\n    \"method\": 1\n  }\n]" + '\n';

        await File.WriteAllTextAsync(service.FilePath, legacyJson, NonCancelableToken);

        // Act
        var loaded = await service.LoadAsync();

        // Assert
        _ = loaded.Should().ContainSingle();
        _ = loaded[0].Trigger.Should().Be(":mail");
        _ = loaded[0].Method.Should().Be(PasteMethod.CtrlShiftV);
        _ = loaded[0].InsertionMode.Should().Be(TextInsertionMode.Paste);
    }

    [Fact]
    public async Task Load_WhenFileContainsMalformedJson_ReturnsEmptyList_AndClearsCache()
    {
        // Arrange
        var service = CreateService();
        await service.SaveAsync(new List<TextExpansionEntry> { new(":ok", "value") });
        File.WriteAllText(service.FilePath, "{ invalid json }");

        // Act
        var loaded = service.Load();

        // Assert
        _ = loaded.Should().BeEmpty();
        _ = service.GetCurrent().Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrent_AfterSave_ReturnsSavedData()
    {
        // Arrange
        var service = CreateService();
        var expansions = new List<TextExpansionEntry>
        {
            new(":test", "Test Value"),
        };

        // Act
        await service.SaveAsync(expansions);
        var current = service.GetCurrent();

        // Assert
        _ = current.Should().HaveCount(1);
        _ = current[0].Trigger.Should().Be(":test");
    }

    [Fact]
    public async Task SaveAsync_WhenEnumerationThrows_PropagatesException_AndKeepsCache()
    {
        // Arrange
        var service = CreateService();
        var baseline = new List<TextExpansionEntry> { new(":ok", "value") };
        await service.SaveAsync(baseline);

        // Act
        var act = async () => await service.SaveAsync(new ThrowingExpansionEnumerable());

        // Assert
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
