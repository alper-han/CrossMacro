
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignMacroFileManager : IMacroFileManager
{
    public Task SaveAsync(MacroSequence macro, string filePath) => Task.CompletedTask;

    public Task<MacroSequence?> LoadAsync(string filePath) => Task.FromResult<MacroSequence?>(DesignPreviewSamples.CreateMacro("Loaded Nightly Export Retry"));
}
