
namespace CrossMacro.Infrastructure.Tests.Services.Automation;

public sealed class AutomationTaskRepositoryTests : IDisposable
{
    private const string Malformed = "[ { not valid json";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CrossMacroTaskRepositoryTests_" + Guid.NewGuid().ToString("N"));

    public AutomationTaskRepositoryTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Triggers_LoadAsync_WhenFileIsMalformed_MovesItAsideAndKeepsItAfterSave()
    {
        var filePath = Path.Combine(_directory, ConfigFileNames.Triggers);
        await File.WriteAllTextAsync(filePath, Malformed, TestContext.Current.CancellationToken);
        var repository = new JsonTriggerTaskRepository(filePath);

        var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);
        await repository.SaveAsync([new TriggerTask { Name = "Fresh" }], TestContext.Current.CancellationToken);

        Assert.Null(loaded);
        await AssertMalformedFilePreservedAsync(ConfigFileNames.Triggers);
        _ = Assert.Single((await repository.LoadAsync(TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Shortcuts_LoadAsync_WhenFileIsMalformed_MovesItAsideAndKeepsItAfterSave()
    {
        var filePath = Path.Combine(_directory, ConfigFileNames.Shortcuts);
        await File.WriteAllTextAsync(filePath, Malformed, TestContext.Current.CancellationToken);
        var repository = new JsonShortcutTaskRepository(filePath, Substitute.For<IShortcutHotkeyNormalizer>());

        var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);
        await repository.SaveAsync([], TestContext.Current.CancellationToken);

        Assert.Null(loaded);
        await AssertMalformedFilePreservedAsync(ConfigFileNames.Shortcuts);
    }

    [Fact]
    public async Task Shortcuts_LoadAsync_WhenHotkeyIsRejected_LeavesTheFileInPlace()
    {
        var filePath = Path.Combine(_directory, ConfigFileNames.Shortcuts);
        const string Content = """[{"name":"Odd","macroFilePath":"a.macro","hotkeys":["Ctrl+Unmapped"]}]""";
        await File.WriteAllTextAsync(filePath, Content, TestContext.Current.CancellationToken);
        var repository = new JsonShortcutTaskRepository(filePath, new RejectingNormalizer());

        _ = await Assert.ThrowsAsync<JsonException>(() => repository.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Content, await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.corrupt"));
    }

    private async Task AssertMalformedFilePreservedAsync(string fileName)
    {
        var preserved = Assert.Single(Directory.EnumerateFiles(_directory, fileName + ".*.corrupt"));
        Assert.Equal(Malformed, await File.ReadAllTextAsync(preserved, TestContext.Current.CancellationToken));
    }

    private sealed class RejectingNormalizer : IShortcutHotkeyNormalizer
    {
        public bool TryNormalize(string? hotkey, out string? normalized, out string? validationMessage)
        {
            normalized = null;
            validationMessage = $"Unsupported hotkey: {hotkey}.";
            return false;
        }
    }
}
