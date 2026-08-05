namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Validates image assets and their script references at the macro file boundary.
/// </summary>
internal sealed class MacroAssetValidator(IImageAssetCodec imageAssetCodec)
{
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? throw new ArgumentNullException(nameof(imageAssetCodec));

    internal async Task<long> AddImageMetadataAsync(
        MacroSequence macro,
        string line,
        long totalEncodedImageChars)
    {
        ArgumentNullException.ThrowIfNull(macro);

        var metadata = line.Substring(MacroFileLimits.ImageHeader.Length);
        var separatorIndex = metadata.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            throw new InvalidDataException("Malformed image metadata: missing '=' separator.");
        }

        var name = metadata[..separatorIndex].Trim();
        var encoded = metadata[(separatorIndex + 1)..].Trim();
        ValidateImageName(name);

        await _imageAssetCodec.ValidateBase64PngAsync(encoded, name, CancellationToken.None).ConfigureAwait(false);
        totalEncodedImageChars = checked(totalEncodedImageChars + encoded.Length);
        _imageAssetCodec.ValidateMacroBudget(totalEncodedImageChars);
        macro.Images[name] = encoded;
        return totalEncodedImageChars;
    }

    internal async Task<List<KeyValuePair<string, string>>> ValidateBeforeSaveAsync(MacroSequence macro)
    {
        ArgumentNullException.ThrowIfNull(macro);

        if (macro.Images is null || macro.Images.Count is 0)
        {
            ValidateReferences(macro.ScriptSteps, new Dictionary<string, string>(StringComparer.Ordinal), "Saved macro");
            return [];
        }

        var imageAssets = new List<KeyValuePair<string, string>>(macro.Images.Count);
        long totalEncodedBytes = 0;
        foreach (var image in macro.Images.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ValidateImageName(image.Key);

            var encoded = image.Value?.Trim();
            if (encoded is null || encoded.Length is 0 || encoded.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException($"Image asset '{image.Key}': Image asset metadata is malformed.");
            }

            await _imageAssetCodec.ValidateBase64PngAsync(encoded, image.Key, CancellationToken.None).ConfigureAwait(false);
            totalEncodedBytes = checked(totalEncodedBytes + encoded.Length);
            imageAssets.Add(new KeyValuePair<string, string>(image.Key, encoded));
        }

        _imageAssetCodec.ValidateMacroBudget(totalEncodedBytes);
        ValidateReferences(macro.ScriptSteps, macro.Images, "Saved macro");
        return imageAssets;
    }

    internal static void ValidateReferences(
        IList<string> scriptSteps,
        IDictionary<string, string> images,
        string context)
    {
        ArgumentNullException.ThrowIfNull(scriptSteps);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        for (var index = 0; index < scriptSteps.Count; index++)
        {
            var step = scriptSteps[index];
            if (!RunScriptScreenReadingStepParser.TryParseCommand(step.Trim(), out var command, out var parts)
                || command is not (RunScriptScreenReadingCommand.ImageSearch or RunScriptScreenReadingCommand.ImageClick or RunScriptScreenReadingCommand.WaitImage))
            {
                continue;
            }

            if (!RunScriptScreenReadingStepParser.TryValidateStep(step, out var error) || error is not null)
            {
                throw new InvalidDataException($"{context} script step {(index + 1).ToString(CultureInfo.InvariantCulture)}: {error ?? "invalid image command"}");
            }

            var imageNameIndex = parts.Length >= 6
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? 5
                : 1;
            var imageName = parts[imageNameIndex];
            if (!images.ContainsKey(imageName))
            {
                throw new InvalidDataException($"{context} script step {(index + 1).ToString(CultureInfo.InvariantCulture)}: image asset '{imageName}' is not defined.");
            }
        }
    }

    private static void ValidateImageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !(char.IsLetter(name[0]) || name[0] == '_')
            || name.Skip(1).Any(character => !(char.IsLetterOrDigit(character) || character == '_')))
        {
            throw new InvalidDataException($"Image asset '{name}': Image asset name is invalid.");
        }
    }
}
