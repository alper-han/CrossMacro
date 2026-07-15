using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Compilation outcome for script steps.
/// </summary>
public sealed class RunScriptCompileResult
{
    private RunScriptCompileResult()
    {
    }

    public bool Success { get; private init; }
    public MacroSequence? Sequence { get; private init; }
    public int InitialDelayMs { get; private init; }
    public bool InitialHasRandomDelay { get; private init; }
    public int InitialRandomDelayMinMs { get; private init; }
    public int InitialRandomDelayMaxMs { get; private init; }
    public string ErrorMessage { get; private init; } = string.Empty;

    public static RunScriptCompileResult Ok(
        MacroSequence sequence,
        int initialDelayMs,
        bool initialHasRandomDelay = false,
        int initialRandomDelayMinMs = 0,
        int initialRandomDelayMaxMs = 0)
    {
        return new RunScriptCompileResult
        {
            Success = true,
            Sequence = sequence,
            InitialDelayMs = initialDelayMs,
            InitialHasRandomDelay = initialHasRandomDelay,
            InitialRandomDelayMinMs = initialRandomDelayMinMs,
            InitialRandomDelayMaxMs = initialRandomDelayMaxMs,
        };
    }

    public static RunScriptCompileResult Fail(string errorMessage)
    {
        return new RunScriptCompileResult
        {
            Success = false,
            ErrorMessage = errorMessage,
        };
    }
}
