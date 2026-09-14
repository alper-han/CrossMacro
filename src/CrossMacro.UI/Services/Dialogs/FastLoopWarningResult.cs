
namespace CrossMacro.UI.Services.Dialogs;

public readonly record struct FastLoopWarningResult(bool ContinuePlayback, bool SuppressFutureWarnings)
{
    public static FastLoopWarningResult Cancelled { get; } = new(ContinuePlayback: false, SuppressFutureWarnings: false);
}
