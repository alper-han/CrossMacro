
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class JsonScheduledTaskRepositoryTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly string _tempFile;
    private readonly JsonScheduledTaskRepository _repository;

    public JsonScheduledTaskRepositoryTests()
    {
        _tempFile = Path.GetTempFileName();
        _repository = new JsonScheduledTaskRepository(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenScheduleFilePathIsMissing_ThrowsArgumentException(string? path)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => new JsonScheduledTaskRepository(path!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ReloadAsync_WhenProfileConfigDirectoryIsMissing_ThrowsArgumentException(string? path)
    {
        _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.ReloadAsync(path!));
    }

    [Fact]
    public async Task SaveAsync_WhenTasksAreNull_ThrowsArgumentNullException()
    {
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!));
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        // Dispose creates the file, so we delete it first to test missing file case
        File.Delete(_tempFile);

        // Act
        var result = await _repository.LoadAsync();

        // Assert
        _ = result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_SavesTasksToFile()
    {
        // Arrange
        var tasks = new List<ScheduledTask>
        {
            new ScheduledTask { Name = "Task 1", MacroFilePath = "path1" },
            new ScheduledTask { Name = "Task 2", MacroFilePath = "path2" },
        };

        // Act
        await _repository.SaveAsync(tasks);

        // Assert
        var loaded = await _repository.LoadAsync();
        _ = loaded.Should().HaveCount(2);
        _ = loaded[0].Name.Should().Be("Task 1");
        _ = loaded[^1].Name.Should().Be("Task 2");
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "schedules.json");
        var repo = new JsonScheduledTaskRepository(filePath);

        try
        {
            var tasks = new List<ScheduledTask> { new ScheduledTask { Name = "Task 1" } };

            // Act
            await repo.SaveAsync(tasks);

            // Assert
            _ = File.Exists(filePath).Should().BeTrue();
            _ = Directory.Exists(tempDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileContainsMalformedJson_ReturnsEmptyList()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFile, "{ invalid json }", NonCancelableToken);

        // Act
        var result = await _repository.LoadAsync();

        // Assert
        _ = result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenWriteFails_Throws()
    {
        // Arrange
        var blockingPath = Path.Combine(_tempFile, "schedules.json");
        var repository = new JsonScheduledTaskRepository(blockingPath);
        var tasks = new[] { new ScheduledTask { Name = "Task 1" } };

        // Act
        var act = async () => await repository.SaveAsync(tasks);

        // Assert
        _ = await act.Should().ThrowAsync<IOException>();
    }
}
