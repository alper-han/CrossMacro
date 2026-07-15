
namespace CrossMacro.Cli.Services;

public sealed class ClipboardCliService : IClipboardCliService
{
    private readonly IClipboardService? _clipboardService;

    public ClipboardCliService(IClipboardService? clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public async Task<CliCommandExecutionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var clipboardService, out var unsupported))
        {
            return unsupported;
        }

        try
        {
            var value = await clipboardService.GetTextAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
            return CliCommandExecutionResult.Ok("Clipboard text read.", new ClipboardTextData(value));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Failed to read clipboard text.", [ex.Message]);
        }
    }

    public async Task<CliCommandExecutionResult> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var clipboardService, out var unsupported))
        {
            return unsupported;
        }

        return await SetResolvedTextAsync(clipboardService, text, "text", cancellationToken).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> SetFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var clipboardService, out var unsupported))
        {
            return unsupported;
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Failed to read clipboard input file.", [ex.Message]);
        }

        return await SetResolvedTextAsync(clipboardService, text, filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> ClearAsync(CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var clipboardService, out var unsupported))
        {
            return unsupported;
        }

        return await SetResolvedTextAsync(clipboardService, string.Empty, "clear", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CliCommandExecutionResult> SetResolvedTextAsync(IClipboardService clipboardService, string text, string source, CancellationToken cancellationToken)
    {
        try
        {
            await clipboardService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok("Clipboard text set.", new ClipboardSetData(text.Length, source));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Failed to set clipboard text.", [ex.Message]);
        }
    }

    private bool TryGetClipboardService(
        [NotNullWhen(true)] out IClipboardService? clipboardService,
        [NotNullWhen(false)] out CliCommandExecutionResult? result)
    {
        if (_clipboardService is null || !_clipboardService.IsSupported)
        {
            clipboardService = null;
            result = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Clipboard text is not supported in this runtime.",
                ["No supported IClipboardService is available for the current platform/session."]);
            return false;
        }

        clipboardService = _clipboardService;
        result = null;
        return true;
    }
}
