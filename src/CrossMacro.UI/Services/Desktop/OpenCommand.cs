
namespace CrossMacro.UI.Services.Desktop;

internal readonly record struct OpenCommand(string FileName, params string[] ArgumentsBeforeTarget);
