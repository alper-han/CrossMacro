
using CrossMacro.Infrastructure.Persistence.Macros;
using static CrossMacro.Infrastructure.Persistence.Macros.MacroFileLimits;

namespace CrossMacro.Infrastructure.Services.Macros;

/// <summary>
/// Handles saving and loading macro sequences from files
/// </summary>
public class MacroFileManager(
    Func<IKeyCodeMapper> keyCodeMapperFactory,
    IImageAssetCodec? imageAssetCodec = null,
    IScriptValidationService? scriptValidationService = null) : IMacroFileManager
{
    private readonly Func<IKeyCodeMapper> _keyCodeMapperFactory = keyCodeMapperFactory ?? throw new ArgumentNullException(nameof(keyCodeMapperFactory));
    private readonly IScriptValidationService? _scriptValidationService = scriptValidationService;
    private readonly MacroAssetValidator _assetValidator = new(imageAssetCodec ?? new ImageAssetCodec());

    /// <summary>
    /// Saves a macro sequence to a custom text file (.macro)
    /// </summary>
    public async Task SaveAsync(MacroSequence macro, string filePath)
    {
        ArgumentNullException.ThrowIfNull(macro);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!macro.IsValid())
        {
            throw new InvalidOperationException("Cannot save invalid macro sequence");
        }

        if (macro.Name.Any(char.IsControl))
        {
            throw new InvalidOperationException("Macro name cannot contain control characters");
        }

        ValidateScriptStepsBeforeSave(macro);
        ValidateCountsBeforeSave(macro);
        var imageAssets = await _assetValidator.ValidateBeforeSaveAsync(macro).ConfigureAwait(false);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await WriteTemporaryFileAsync(macro, imageAssets, temporaryPath).ConfigureAwait(false);

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, filePath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Loads a macro sequence from a custom text file (.macro)
    /// </summary>
    public async Task<MacroSequence?> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Macro file not found", filePath);
        }

        ValidateMacroFile(filePath);
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var fileStreamDisposal = fileStream.ConfigureAwait(false);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
        return await new MacroFileReader(_assetValidator, reader).ReadAsync().ConfigureAwait(false);
    }

    private static async Task WriteTemporaryFileAsync(
        MacroSequence macro,
        IReadOnlyList<KeyValuePair<string, string>> imageAssets,
        string temporaryPath)
    {
        var temporaryStream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            65536,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using (temporaryStream.ConfigureAwait(false))
        {
            using (var writer = new StreamWriter(temporaryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true))
            {
                await MacroFileWriter.WriteAsync(writer, macro, imageAssets).ConfigureAwait(false);
            }

            await temporaryStream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            if (temporaryStream.Length > MaxMacroFileBytes)
            {
                throw new InvalidOperationException(
                    $"Macro is too large to save: the file would exceed {(MaxMacroFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture)} MB. Remove events or embedded images and try again.");
            }
        }
    }

    /// <summary>A file the loader would reject must never be written, so the user learns at save time.</summary>
    private static void ValidateCountsBeforeSave(MacroSequence macro)
    {
        if (macro.Events.Count > MaxMacroEvents)
        {
            throw new InvalidOperationException(
                $"Macro has {macro.Events.Count.ToString(CultureInfo.InvariantCulture)} events; at most {MaxMacroEvents.ToString(CultureInfo.InvariantCulture)} can be saved. Remove events and try again.");
        }

        if (macro.ScriptSteps.Count > MaxMacroScriptSteps)
        {
            throw new InvalidOperationException(
                $"Macro has {macro.ScriptSteps.Count.ToString(CultureInfo.InvariantCulture)} script steps; at most {MaxMacroScriptSteps.ToString(CultureInfo.InvariantCulture)} can be saved.");
        }
    }

    private void ValidateScriptStepsBeforeSave(MacroSequence macro)
    {
        if (macro.ScriptSteps is null)
        {
            return;
        }

        var steps = new List<RunScriptStep>(macro.ScriptSteps.Count);
        for (var index = 0; index < macro.ScriptSteps.Count; index++)
        {
            var step = macro.ScriptSteps[index];
            if (string.IsNullOrWhiteSpace(step))
            {
                continue;
            }

            steps.Add(new RunScriptStep(step, SourceIndex: index));
        }

        if (steps.Count is 0)
        {
            return;
        }

        var validationService = _scriptValidationService ?? new ScriptValidationService(_keyCodeMapperFactory());
        var diagnostics = validationService.Validate(steps);
        var diagnostic = diagnostics.Count > 0 ? diagnostics[0] : null;
        if (diagnostic is not null)
        {
            throw new InvalidOperationException($"Cannot save invalid macro script steps: {diagnostic.Message}");
        }
    }

    private static void ValidateMacroFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
            || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("Macro path must refer to a regular file.");
        }

        long length;
        try
        {
            length = fileInfo.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidDataException("Macro path must refer to a regular file.", ex);
        }

        if (length is <= 0 or > MaxMacroFileBytes)
        {
            throw new InvalidDataException(length <= 0
                ? "Macro file is empty."
                : $"Macro file exceeds the maximum size of {MaxMacroFileBytes} bytes.");
        }
    }
}
