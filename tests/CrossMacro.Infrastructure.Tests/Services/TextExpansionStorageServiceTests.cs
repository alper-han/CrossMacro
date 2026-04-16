namespace CrossMacro.Infrastructure.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;

public class TextExpansionStorageServiceTests : IDisposable
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
        Directory.CreateDirectory(_testRootDirectory);
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
        catch { /* Ignore cleanup errors */ }
    }

    private TextExpansionStorageService CreateService()
    {
        var serviceDirectory = Path.Combine(_testRootDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(serviceDirectory);
        return new TextExpansionStorageService(serviceDirectory);
    }

    [Fact]
    public void FilePath_IsNotEmpty()
    {
        // Assert
        _service.FilePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FilePath_ContainsCrossmacro()
    {
        // Assert
        _service.FilePath.Should().Contain(_testRootDirectory);
    }

    [Fact]
    public void FilePath_EndsWithJson()
    {
        // Assert
        _service.FilePath.Should().EndWith(".json");
    }

    [Fact]
    public void GetCurrent_Initially_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetCurrent();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Load_WhenFileNotExists_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act (file likely doesn't exist in test environment)
        var result = service.Load();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenFileNotExists_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.LoadAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_NullList_ThrowsException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = async () => await service.SaveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_EmptyList_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        var emptyList = new List<TextExpansion>();

        // Act
        var act = async () => await service.SaveAsync(emptyList);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        // Arrange
        var service = CreateService();
        var expansions = new List<TextExpansion>
        {
            new(":mail", "test@example.com"),
            new(":sig", "Best regards,\nTest User", true, PasteMethod.ShiftInsert, TextInsertionMode.DirectTyping)
        };

        // Act
        await service.SaveAsync(expansions);
        var loaded = await service.LoadAsync();

        // Assert
        loaded.Should().HaveCount(2);
        loaded[0].Trigger.Should().Be(":mail");
        loaded[0].Replacement.Should().Be("test@example.com");
        loaded[1].Trigger.Should().Be(":sig");
        loaded[1].Method.Should().Be(PasteMethod.ShiftInsert);
        loaded[1].InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
    }

    [Fact]
    public async Task LoadAsync_WhenInsertionModeIsMissing_DefaultsToPaste()
    {
        // Arrange
        var service = CreateService();
        var legacyJson = """
            [
              {
                "trigger": ":mail",
                "replacement": "test@example.com",
                "isEnabled": true,
                "method": 1
              }
            ]
            """;

        await File.WriteAllTextAsync(service.FilePath, legacyJson);

        // Act
        var loaded = await service.LoadAsync();

        // Assert
        loaded.Should().ContainSingle();
        loaded[0].Trigger.Should().Be(":mail");
        loaded[0].Method.Should().Be(PasteMethod.CtrlShiftV);
        loaded[0].InsertionMode.Should().Be(TextInsertionMode.Paste);
    }

    [Fact]
    public async Task GetCurrent_AfterSave_ReturnsSavedData()
    {
        // Arrange
        var service = CreateService();
        var expansions = new List<TextExpansion>
        {
            new(":test", "Test Value")
        };

        // Act
        await service.SaveAsync(expansions);
        var current = service.GetCurrent();

        // Assert
        current.Should().HaveCount(1);
        current[0].Trigger.Should().Be(":test");
    }

    [Fact]
    public async Task SaveAsync_WhenEnumerationThrows_PropagatesException_AndKeepsCache()
    {
        // Arrange
        var service = CreateService();
        var baseline = new List<TextExpansion> { new(":ok", "value") };
        await service.SaveAsync(baseline);

        // Act
        var act = async () => await service.SaveAsync(new ThrowingExpansionEnumerable());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("enumeration failed");

        var current = service.GetCurrent();
        current.Should().HaveCount(1);
        current[0].Trigger.Should().Be(":ok");
    }

    private sealed class ThrowingExpansionEnumerable : IEnumerable<TextExpansion>
    {
        public IEnumerator<TextExpansion> GetEnumerator() => throw new InvalidOperationException("enumeration failed");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
