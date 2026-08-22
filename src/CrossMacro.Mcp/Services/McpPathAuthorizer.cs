namespace CrossMacro.Mcp.Services;

public sealed class McpPathAuthorizer(IMcpPathPolicy pathPolicy)
{
    private const long MaximumScreenImageBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes;

    private readonly IMcpPathPolicy _pathPolicy = pathPolicy;

    public bool TryNormalizeDirectoryPath(string directoryPath, out string normalizedDirectoryPath, out McpToolOutcome error)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            normalizedDirectoryPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Directory path is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(directoryPath))
        {
            normalizedDirectoryPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Directory path must be absolute.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(directoryPath, McpPathKind.MacroRead, requireExisting: true, out normalizedDirectoryPath, out error))
            {
                return false;
            }

            var directoryInfo = new DirectoryInfo(normalizedDirectoryPath);
            if (!directoryInfo.Exists)
            {
                error = McpToolOutcomeMapper.FileError("Macro directory not found.");
                return false;
            }

            if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Macro directory must not be a symbolic link.");
                return false;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            normalizedDirectoryPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Directory path is invalid.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    public bool TryNormalizeMacroPath(string macroPath, out string normalizedMacroPath, out McpToolOutcome error)
    {
        if (string.IsNullOrWhiteSpace(macroPath))
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(macroPath))
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(macroPath), ".macro", StringComparison.OrdinalIgnoreCase))
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path must use the .macro extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(macroPath, McpPathKind.MacroRead, requireExisting: true, out normalizedMacroPath, out error))
            {
                return false;
            }

            if (!File.Exists(normalizedMacroPath))
            {
                error = McpToolOutcomeMapper.FileError("Macro file not found.");
                return false;
            }

            var fileInfo = new FileInfo(normalizedMacroPath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Macro path must refer to a regular file.");
                return false;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path is invalid.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    public bool TryNormalizeOptionalMacroPath(string? macroPath, out string? normalizedMacroPath, out McpToolOutcome error)
    {
        normalizedMacroPath = macroPath;
        if (string.IsNullOrWhiteSpace(macroPath))
        {
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (!TryNormalizeMacroPath(macroPath, out var normalizedPath, out error))
        {
            normalizedMacroPath = null;
            return false;
        }

        normalizedMacroPath = normalizedPath;
        return true;
    }

    public bool TryNormalizeRecordingOutputPath(string? outputPath, out string normalizedOutputPath, out McpToolOutcome error)
    {
        normalizedOutputPath = string.Empty;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(outputPath), ".macro", StringComparison.OrdinalIgnoreCase))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must use the .macro extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(outputPath, McpPathKind.MacroWrite, requireExisting: false, out normalizedOutputPath, out error))
            {
                return false;
            }

            if (Directory.Exists(normalizedOutputPath))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must refer to a file.");
                return false;
            }

            if (File.Exists(normalizedOutputPath)
                && new FileInfo(normalizedOutputPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must not be a symbolic link.");
                return false;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error = McpToolOutcomeMapper.FileError("Recording outputPath could not be accessed.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    public bool TryNormalizeScreenImagePath(string imagePath, out string normalizedImagePath, out McpToolOutcome error)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Screen image path is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(imagePath))
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Screen image path must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(imagePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Screen image path must use the .png extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(imagePath, McpPathKind.ImageRead, requireExisting: true, out normalizedImagePath, out error))
            {
                return false;
            }

            if (!File.Exists(normalizedImagePath))
            {
                error = McpToolOutcomeMapper.FileError("Screen image file not found.");
                return false;
            }

            var fileInfo = new FileInfo(normalizedImagePath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screen image path must refer to a regular file.");
                return false;
            }

            if (fileInfo.Length is <= 0 or > MaximumScreenImageBytes)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screen image file exceeds the allowed size.");
                return false;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.FileError("Screen image file could not be accessed.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    public bool TryNormalizeScreenshotOutputPath(string? outputPath, out string? normalizedOutputPath, out McpToolOutcome error)
    {
        normalizedOutputPath = null;
        if (outputPath is null)
        {
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must not be empty.");
            return false;
        }

        if (!Path.IsPathFullyQualified(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must use the .png extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(outputPath, McpPathKind.ImageWrite, requireExisting: false, out var authorizedPath, out error))
            {
                return false;
            }

            normalizedOutputPath = authorizedPath;
            if (Directory.Exists(normalizedOutputPath))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must refer to a file.");
                return false;
            }

            if (File.Exists(normalizedOutputPath)
                && new FileInfo(normalizedOutputPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must not be a symbolic link.");
                return false;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            normalizedOutputPath = null;
            error = McpToolOutcomeMapper.FileError("Screenshot output path could not be accessed.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    public bool TryAuthorizeImageOrMacroReadPath(string path, out string normalizedPath, out McpToolOutcome error)
    {
        var kind = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".macro" => McpPathKind.MacroRead,
            ".png" => McpPathKind.ImageRead,
            _ => McpPathKind.FileRead,
        };
        return _pathPolicy.TryAuthorize(path, kind, requireExisting: true, out normalizedPath, out error);
    }

    public bool TryAuthorizeFileReadPath(string path, out string normalizedPath, out McpToolOutcome error) =>
        _pathPolicy.TryAuthorize(path, McpPathKind.FileRead, requireExisting: true, out normalizedPath, out error);
}
