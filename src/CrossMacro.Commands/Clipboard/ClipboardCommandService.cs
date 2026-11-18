
namespace CrossMacro.Commands.Clipboard;

public sealed class ClipboardCommandService(IClipboardService? clipboardService) : IClipboardCommandService
{
    private readonly IClipboardService? _clipboardService = clipboardService;

    public async Task<OperationResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var resolvedClipboardService, out var unsupported))
        {
            return unsupported;
        }

        try
        {
            var value = await resolvedClipboardService.GetTextAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
            return OperationResult.Ok("Clipboard text read.", new ClipboardTextData(value));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Failed to read clipboard text.", [ex.Message]);
        }
    }

    public async Task<OperationResult> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > ClipboardTextLimits.MaximumCharacters)
        {
            return TooLong();
        }

        if (!TryGetClipboardService(out var resolvedClipboardService, out var unsupported))
        {
            return unsupported;
        }

        return await SetResolvedTextAsync(resolvedClipboardService, text, "text", cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SetFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var resolvedClipboardService, out var unsupported))
        {
            return unsupported;
        }

        string text;
        try
        {
            text = await ReadBoundedTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return TooLong();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Failed to read clipboard input file.", [ex.Message]);
        }

        return await SetResolvedTextAsync(resolvedClipboardService, text, filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> ClearAsync(CancellationToken cancellationToken)
    {
        if (!TryGetClipboardService(out var resolvedClipboardService, out var unsupported))
        {
            return unsupported;
        }

        return await SetResolvedTextAsync(resolvedClipboardService, string.Empty, "clear", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationResult> SetResolvedTextAsync(IClipboardService clipboardService, string text, string source, CancellationToken cancellationToken)
    {
        try
        {
            await clipboardService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok("Clipboard text set.", new ClipboardSetData(text.Length, source));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Failed to set clipboard text.", [ex.Message]);
        }
    }

    private static OperationResult TooLong() =>
        OperationResult.Fail(
            ExecutionOutcomeCode.InvalidArguments,
            "Clipboard text exceeds the maximum allowed length.",
            [string.Create(CultureInfo.InvariantCulture, $"Clipboard text is limited to {ClipboardTextLimits.MaximumCharacters} characters.")]);

    private static async Task<string> ReadBoundedTextAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[ClipboardTextLimits.MaximumCharacters + 1];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return count > ClipboardTextLimits.MaximumCharacters
            ? throw new InvalidDataException("The file exceeds the clipboard text limit.")
            : new string(buffer, 0, count);
    }

    private bool TryGetClipboardService(
        [NotNullWhen(true)] out IClipboardService? clipboardService,
        [NotNullWhen(false)] out OperationResult? result)
    {
        if (_clipboardService is null || !_clipboardService.IsSupported)
        {
            clipboardService = null;
            result = OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Clipboard text is not supported in this runtime.",
                ["No supported IClipboardService is available for the current platform/session."]);
            return false;
        }

        clipboardService = _clipboardService;
        result = null;
        return true;
    }
}
