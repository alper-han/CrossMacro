using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class JsonScheduledTaskRepositoryTests : IDisposable
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

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        // Dispose creates the file, so we delete it first to test missing file case
        File.Delete(_tempFile);

        // Act
        var result = await _repository.LoadAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_SavesTasksToFile()
    {
        // Arrange
        var tasks = new List<ScheduledTask>
        {
            new ScheduledTask { Name = "Task 1", MacroFilePath = "path1" },
            new ScheduledTask { Name = "Task 2", MacroFilePath = "path2" }
        };

        // Act
        await _repository.SaveAsync(tasks);

        // Assert
        var loaded = await _repository.LoadAsync();
        loaded.Should().HaveCount(2);
        loaded.First().Name.Should().Be("Task 1");
        loaded.Last().Name.Should().Be("Task 2");
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
            File.Exists(filePath).Should().BeTrue();
            Directory.Exists(tempDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileContainsMalformedJson_ReturnsEmptyList()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFile, "{ invalid json }");

        // Act
        var result = await _repository.LoadAsync();

        // Assert
        result.Should().BeEmpty();
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
        await act.Should().ThrowAsync<IOException>();
    }
}
