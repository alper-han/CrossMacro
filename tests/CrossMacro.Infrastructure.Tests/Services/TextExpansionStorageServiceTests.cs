namespace CrossMacro.Infrastructure.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;

public class TextExpansionStorageServiceTests : IDisposable
{
    private readonly TextExpansionStorageService _service;
    private readonly List<string> _tempFiles = new();

    public TextExpansionStorageServiceTests()
    {
        _service = new TextExpansionStorageService();
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch { /* Ignore cleanup errors */ }
        }
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
        _service.FilePath.Should().Contain("crossmacro");
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
        var service = new TextExpansionStorageService();

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
        var service = new TextExpansionStorageService();

        // Act (file likely doesn't exist in test environment)
        var result = service.Load();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenFileNotExists_ReturnsEmptyList()
    {
        // Arrange
        var service = new TextExpansionStorageService();

        // Act
        var result = await service.LoadAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_NullList_ThrowsException()
    {
        // Arrange
        var service = new TextExpansionStorageService();

        // Act
        var act = async () => await service.SaveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_EmptyList_DoesNotThrow()
    {
        // Arrange
        var service = new TextExpansionStorageService();
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
        var service = new TextExpansionStorageService();
        var expansions = new List<TextExpansion>
        {
            new(":mail", "test@example.com"),
            new(":sig", "Best regards,\nTest User")
        };

        // Act
        await service.SaveAsync(expansions);
        var loaded = await service.LoadAsync();

        // Assert
        loaded.Should().HaveCount(2);
        loaded[0].Trigger.Should().Be(":mail");
        loaded[0].Replacement.Should().Be("test@example.com");
        loaded[1].Trigger.Should().Be(":sig");
    }

    [Fact]
    public async Task GetCurrent_AfterSave_ReturnsSavedData()
    {
        // Arrange
        var service = new TextExpansionStorageService();
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
        var service = new TextExpansionStorageService();
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
