
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// COSMIC provider for output geometry. Cursor position is intentionally unsupported
/// because COSMIC does not currently expose a stable public cursor-position API.
/// </summary>
public sealed partial class CosmicPositionProvider : IMousePositionProvider
{
    private const string CosmicRandrCommand = "cosmic-randr";
    private const int CommandTimeoutMs = 1000;

    private readonly Func<CancellationToken, Task<string?>> _readOutputTopologyAsync;
    private bool _disposed;

    public CosmicPositionProvider()
        : this(ReadCosmicRandrKdlAsync) { /* Empty */ }

    internal CosmicPositionProvider(Func<CancellationToken, Task<string?>> readOutputTopologyAsync)
    {
        _readOutputTopologyAsync = readOutputTopologyAsync ?? throw new ArgumentNullException(nameof(readOutputTopologyAsync));
    }

    public string ProviderName => "COSMIC RandR (Resolution Only)";

    public bool IsSupported => false;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        return Task.FromResult<(int X, int Y)?>(null);
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(CommandTimeoutMs);
            var output = await _readOutputTopologyAsync(timeoutCts.Token).ConfigureAwait(false);
            if (TryParseScreenResolution(output, out var width, out var height))
            {
                Log.Information("[CosmicPositionProvider] Screen resolution detected: {Width}x{Height}", width, height);
                return (width, height);
            }

            Log.Warning("[CosmicPositionProvider] Failed to parse screen resolution from cosmic-randr output");
            return null;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[CosmicPositionProvider] Timed out while querying cosmic-randr output topology");
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[CosmicPositionProvider] Failed to get screen resolution");
            return null;
        }
    }

    internal static bool TryParseScreenResolution(string? kdl, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(kdl))
        {
            return false;
        }

        try
        {
            var outputs = ParseOutputs(kdl);
            var usableOutputs = outputs
                .Where(static output => output.Enabled &&
                                        !output.IsMirrored &&
output.Position is not null &&
output.CurrentMode is not null &&
                                        output.Scale > 0)
                .Select(static output => output.ToLogicalRectangle())
                .Where(static rectangle => rectangle.Width > 0 && rectangle.Height > 0)
                .ToArray();

            if (usableOutputs.Length is 0)
            {
                return false;
            }

            var minX = usableOutputs.Min(static rectangle => rectangle.X);
            var minY = usableOutputs.Min(static rectangle => rectangle.Y);
            var maxX = usableOutputs.Max(static rectangle => rectangle.X + rectangle.Width);
            var maxY = usableOutputs.Max(static rectangle => rectangle.Y + rectangle.Height);

            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            width = maxX - minX;
            height = maxY - minY;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static List<CosmicOutput> ParseOutputs(string kdl)
    {
        var outputs = new List<CosmicOutput>();
        CosmicOutput? currentOutput = null;
        var inModes = false;

        foreach (var rawLine in kdl.Split('\n', StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (line.Length is 0)
            {
                continue;
            }

            if (TryStartNewOutput(line, ref currentOutput, outputs, ref inModes))
            {
                continue;
            }

            if (currentOutput is null)
            {
                continue;
            }

            if (string.Equals(line, "modes {", StringComparison.Ordinal))
            {
                inModes = true;
                continue;
            }

            if (string.Equals(line, "}", StringComparison.Ordinal))
            {
                if (inModes)
                {
                    inModes = false;
                }
                else
                {
                    currentOutput = null;
                }
                continue;
            }

            TryApplyOutputLineProperty(line, currentOutput, inModes);
        }

        return outputs;
    }

    private static bool TryStartNewOutput(string line, ref CosmicOutput? currentOutput, List<CosmicOutput> outputs, ref bool inModes)
    {
        var outputMatch = OutputLineRegex.Match(line);
        if (!outputMatch.Success)
        {
            return false;
        }

        currentOutput = new CosmicOutput(
            enabled: string.Equals(outputMatch.Groups["enabled"].Value, "true", StringComparison.OrdinalIgnoreCase));
        outputs.Add(currentOutput);
        inModes = false;
        return true;
    }

    private static void TryApplyOutputLineProperty(string line, CosmicOutput currentOutput, bool inModes)
    {
        if (line.StartsWith("mirroring ", StringComparison.Ordinal))
        {
            currentOutput.IsMirrored = true;
            return;
        }

        var positionMatch = PositionLineRegex.Match(line);
        if (positionMatch.Success)
        {
            currentOutput.Position = (
                ParseInt32(positionMatch.Groups["x"].Value),
                ParseInt32(positionMatch.Groups["y"].Value));
            return;
        }

        var scaleMatch = ScaleLineRegex.Match(line);
        if (scaleMatch.Success && double.TryParse(scaleMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        {
            currentOutput.Scale = scale;
            return;
        }

        var transformMatch = TransformLineRegex.Match(line);
        if (transformMatch.Success)
        {
            currentOutput.Transform = transformMatch.Groups["transform"].Value;
            return;
        }

        var modeMatch = ModeLineRegex.Match(line);
        if (inModes && modeMatch.Success)
        {
            currentOutput.CurrentMode = (
                ParseInt32(modeMatch.Groups["width"].Value),
                ParseInt32(modeMatch.Groups["height"].Value));
        }
    }

    private static int ParseInt32(string value)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ReadCosmicRandrKdlAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = CosmicRandrCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--kdl");

        using var process = new Process { StartInfo = startInfo };

        try
        {
            _ = process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode is 0)
            {
                return stdout;
            }

            Log.Warning("[CosmicPositionProvider] cosmic-randr exited with code {ExitCode}: {Error}", process.ExitCode, FirstNonEmptyLine(stderr));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Log.Debug("[CosmicPositionProvider] cosmic-randr command not found");
        }
        catch (OperationCanceledException) when (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[CosmicPositionProvider] Failed to kill timed-out cosmic-randr process");
            }

            throw;
        }

        return null;
    }

    private static string? FirstNonEmptyLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return content.Split('\n', StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex("^output\\s+\\\"(?<name>[^\\\"]+)\\\"\\s+enabled=#(?<enabled>true|false)\\s*\\{", RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex OutputLineRegex { get; }

    [GeneratedRegex("^position\\s+(?<x>-?\\d+)\\s+(?<y>-?\\d+)$", RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex PositionLineRegex { get; }

    [GeneratedRegex("^scale\\s+(?<value>[0-9]+(?:\\.[0-9]+)?)$", RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex ScaleLineRegex { get; }

    [GeneratedRegex("^transform\\s+\\\"(?<transform>[^\\\"]+)\\\"$", RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex TransformLineRegex { get; }

    [GeneratedRegex("^mode\\s+(?<width>\\d+)\\s+(?<height>\\d+)\\s+\\d+\\b.*\\bcurrent=#true\\b", RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex ModeLineRegex { get; }

    private sealed class CosmicOutput(bool enabled)
    {
        public bool Enabled { get; } = enabled;
        public bool IsMirrored { get; set; }
        public (int X, int Y)? Position { get; set; }
        public (int Width, int Height)? CurrentMode { get; set; }
        public double Scale { get; set; } = 1.0;
        public string? Transform { get; set; }

        public LogicalRectangle ToLogicalRectangle()
        {
            var (modeWidth, modeHeight) = CurrentMode ?? throw new InvalidOperationException("CurrentMode is not set.");
            if (IsQuarterTurn(Transform))
            {
                (modeWidth, modeHeight) = (modeHeight, modeWidth);
            }

            var width = (int)Math.Round(modeWidth / Scale, MidpointRounding.AwayFromZero);
            var height = (int)Math.Round(modeHeight / Scale, MidpointRounding.AwayFromZero);
            var (x, y) = Position ?? throw new InvalidOperationException("Position is not set.");

            return new LogicalRectangle(x, y, width, height);
        }

        private static bool IsQuarterTurn(string? transform)
        {
            return transform is not null &&
                   (transform.Equals("90", StringComparison.OrdinalIgnoreCase) ||
                    transform.Equals("270", StringComparison.OrdinalIgnoreCase) ||
                    transform.Equals("rotate90", StringComparison.OrdinalIgnoreCase) ||
                    transform.Equals("rotate270", StringComparison.OrdinalIgnoreCase) ||
                    transform.Equals("flipped90", StringComparison.OrdinalIgnoreCase) ||
                    transform.Equals("flipped270", StringComparison.OrdinalIgnoreCase));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LogicalRectangle(int X, int Y, int Width, int Height);
}
