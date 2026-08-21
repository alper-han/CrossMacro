namespace CrossMacro.Infrastructure.Services.ScreenCapture;


public static class ScreenImageAssetPolicy
{
    public const int MaxWidth = 7680;
    public const int MaxHeight = 4320;
    public const int MaxPixelCount = MaxWidth * MaxHeight;
    public const int MaxEncodedBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes;
    public const int MaxBase64Chars = checked((MaxEncodedBytes + 2) / 3 * 4);
    public const int MaxInflatedBytes = 160 * 1024 * 1024;
    public const int MaxPixelBytes = 160 * 1024 * 1024;
    public const int MaxRgbBytes = MaxPixelBytes;
    public const long MaxMacroEncodedBytes = 96L * 1024 * 1024;

    public static void ValidateFileLength(string filePath, string? assetName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var length = new FileInfo(filePath).Length;
        if (length is <= 0 or > MaxEncodedBytes)
        {
            throw new InvalidDataException(FormatMessage(
                length <= 0 ? "Image asset is empty." : $"Image asset exceeds the maximum encoded size of {MaxEncodedBytes} bytes.",
                assetName));
        }
    }

    public static void ValidateBase64Length(string? encoded, string? assetName = null)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidDataException(FormatMessage("Image asset is empty.", assetName));
        }

        var length = encoded.Trim().Length;
        if (length > MaxBase64Chars)
        {
            throw new InvalidDataException(FormatMessage(
                $"Image asset exceeds the maximum Base64 size of {MaxBase64Chars} characters.", assetName));
        }
    }

    public static async Task<byte[]> DecodeValidatedBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default)
    {
        ValidateBase64Length(encoded, assetName);
        var normalized = encoded.Trim();
        byte[] pngBytes;
        try
        {
            pngBytes = Convert.FromBase64String(normalized);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(FormatMessage("Image asset is not valid Base64.", assetName), ex);
        }

        var validation = await TryValidateEncodedPngAsync(pngBytes, assetName, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(validation.Error ?? FormatMessage("Image asset is not a supported PNG.", assetName));
        }

        return pngBytes;
    }

    public static byte[] DecodeValidatedBase64Png(string encoded, string? assetName = null)
    {
        ValidateBase64Length(encoded, assetName);
        var normalized = encoded.Trim();
        byte[] pngBytes;
        try
        {
            pngBytes = Convert.FromBase64String(normalized);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(FormatMessage("Image asset is not valid Base64.", assetName), ex);
        }

        if (!TryValidateEncodedPng(pngBytes, out var error, assetName))
        {
            throw new InvalidDataException(error ?? FormatMessage("Image asset is not a supported PNG.", assetName));
        }

        return pngBytes;
    }

    public static void ValidatePng(ReadOnlySpan<byte> pngBytes, string? assetName = null)
    {
        if (!TryValidateEncodedPng(pngBytes, out var error, assetName))
        {
            throw new InvalidDataException(error ?? FormatMessage("Image asset is not a supported PNG.", assetName));
        }
    }

    public static bool TryValidateEncodedPng(ReadOnlySpan<byte> pngBytes, out string? error, string? assetName = null)
    {
        error = null;
        try
        {
            ValidateEncodedSize(pngBytes.Length, assetName);
            if (!ScreenFramePngDecoder.TryValidatePng(pngBytes, out var width, out var height, out var dimensionError))
            {
                error = FormatMessage(dimensionError ?? "Image asset is not a supported PNG.", assetName);
                return false;
            }

            ValidateDimensions(width, height, assetName);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = FormatMessage(ex.Message, assetName);
            return false;
        }
    }

    public static async Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateEncodedSize(pngBytes.Length, assetName);
            return await ScreenFramePngDecoder.DecodeAsync(pngBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException(FormatMessage(ex.Message, assetName), ex);
        }
    }

    public static ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null)
    {
        try
        {
            ValidateEncodedSize(pngBytes.Length, assetName);
            return ScreenFramePngDecoder.Decode(pngBytes);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException(FormatMessage(ex.Message, assetName), ex);
        }
    }

    public static void ValidateEncodedSize(int byteCount, string? assetName = null)
    {
        if (byteCount <= 0)
        {
            throw new InvalidDataException(FormatMessage("Image asset is empty.", assetName));
        }

        if (byteCount > MaxEncodedBytes)
        {
            throw new InvalidDataException(FormatMessage(
                $"Image asset exceeds the maximum encoded size of {MaxEncodedBytes} bytes.",
                assetName));
        }
    }

    public static void ValidateDimensions(int width, int height, string? assetName = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException(FormatMessage("Image dimensions must be positive.", assetName));
        }

        if (width > MaxWidth || height > MaxHeight)
        {
            throw new InvalidDataException(FormatMessage(
                $"Image dimensions exceed the maximum supported size of {MaxWidth}x{MaxHeight}.",
                assetName));
        }

        var pixelCount = checked(width * height);
        if (pixelCount > MaxPixelCount)
        {
            throw new InvalidDataException(FormatMessage(
                $"Image pixel count exceeds the maximum supported value of {MaxPixelCount}.",
                assetName));
        }
    }

    public static void ValidateMacroBudget(long totalEncodedBytes)
    {
        if (totalEncodedBytes > MaxMacroEncodedBytes)
        {
            throw new InvalidDataException(
                $"Macro image assets exceed the maximum combined encoded size of {MaxMacroEncodedBytes} bytes.");
        }
    }

    public static async Task<(bool IsValid, string? Error)> TryValidateEncodedPngAsync(
        ReadOnlyMemory<byte> pngBytes,
        string? assetName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateEncodedSize(pngBytes.Length, assetName);
            var validation = await ScreenFramePngDecoder.TryValidatePngAsync(pngBytes, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return (false, FormatMessage(validation.Error ?? "Image asset is not a supported PNG.", assetName));
            }

            ValidateDimensions(validation.Width, validation.Height, assetName);
            return (true, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return (false, FormatMessage(ex.Message, assetName));
        }
    }

    private static string FormatMessage(string message, string? assetName)
    {
        return string.IsNullOrWhiteSpace(assetName) ? message : $"Image asset '{assetName}': {message}";
    }
}
