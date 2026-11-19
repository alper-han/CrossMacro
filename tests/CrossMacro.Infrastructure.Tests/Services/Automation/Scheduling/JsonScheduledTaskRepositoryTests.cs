
namespace CrossMacro.Infrastructure.Tests.Services.Automation.Scheduling;

public sealed class JsonScheduledTaskRepositoryTests : IDisposable
{
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MA0191:Do not use the null-forgiving operator", Justification = "Passing null deliberately verifies the public argument guard.")]
    public async Task SaveAsync_WhenTasksAreNull_ThrowsArgumentNullException()
    {
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Dispose creates the file, so we delete it first to test missing file case
        File.Delete(_tempFile);

        var result = await _repository.LoadAsync();

        _ = result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_SavesTasksToFile()
    {
        var tasks = new List<ScheduledTask>
        {
            new ScheduledTask { Name = "Task 1", MacroFilePath = "path1" },
            new ScheduledTask { Name = "Task 2", MacroFilePath = "path2" },
        };

        await _repository.SaveAsync(tasks, cancellationToken: TestContext.Current.CancellationToken);

        var loaded = await _repository.LoadAsync();
        _ = loaded.Should().HaveCount(2);
        _ = loaded[0].Name.Should().Be("Task 1");
        _ = loaded[^1].Name.Should().Be("Task 2");
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "schedules.json");
        var repo = new JsonScheduledTaskRepository(filePath);

        try
        {
            var tasks = new List<ScheduledTask> { new ScheduledTask { Name = "Task 1" } };

            await repo.SaveAsync(tasks, cancellationToken: TestContext.Current.CancellationToken);

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
    public async Task LoadAsync_WhenFileContainsMalformedJson_MovesFileAsideSoItIsNotOverwritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CrossMacroScheduleTests_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        try
        {
            var filePath = Path.Combine(directory, ConfigFileNames.Schedules);
            const string Malformed = "{ invalid json }";
            await File.WriteAllTextAsync(filePath, Malformed, TestContext.Current.CancellationToken);
            var repository = new JsonScheduledTaskRepository(filePath);

            var result = await repository.LoadAsync();
            await repository.SaveAsync([new ScheduledTask { Name = "New task" }], TestContext.Current.CancellationToken);

            _ = result.Should().BeEmpty();
            var preserved = Assert.Single(Directory.EnumerateFiles(directory, ConfigFileNames.Schedules + ".*.corrupt"));
            Assert.Equal(Malformed, await File.ReadAllTextAsync(preserved, TestContext.Current.CancellationToken));
            _ = Assert.Single(await repository.LoadAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileCannotBeRead_Throws()
    {
        await _repository.SaveAsync([new ScheduledTask { Name = "Keep me" }], TestContext.Current.CancellationToken);

        using var exclusive = new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        _ = await Assert.ThrowsAsync<IOException>(() => _repository.LoadAsync());
    }

    [Fact]
    public async Task SaveAsync_WhenWriteFails_Throws()
    {
        var blockingPath = Path.Combine(_tempFile, "schedules.json");
        var repository = new JsonScheduledTaskRepository(blockingPath);
        var tasks = new[] { new ScheduledTask { Name = "Task 1" } };

        var act = async () => await repository.SaveAsync(tasks, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<IOException>();
    }
}
