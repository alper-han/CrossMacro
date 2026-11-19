using System.Globalization;
using System.Text;

namespace CrossMacro.Application.Execution;

internal static class RunStepLoader
{
    private const long MaxStepFileBytes = 2L * 1024 * 1024 * 1024;
    private const int MaxStepLineChars = 128 * 1024 * 1024;
    private const int MaxStepFileLines = 10_000_000;
    private const int MaxSteps = 10_000_000;

    public static async Task<RunStepLoadOutcome> LoadAsync(RunScriptExecutionRequest request, CancellationToken cancellationToken)
    {
        var steps = new List<RunStepEntry>();
        var sourceIndex = 0;
        if (!string.IsNullOrWhiteSpace(request.StepFilePath))
        {
            if (!File.Exists(request.StepFilePath))
            {
                return Fail("Run steps file not found.", $"File does not exist: {request.StepFilePath}");
            }

            try
            {
                sourceIndex = await ReadStepFileAsync(request.StepFilePath, steps, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or DecoderFallbackException)
            {
                return Fail("Failed to read run steps file.", ex.Message);
            }
        }

        foreach (var step in request.Steps)
        {
            if (steps.Count >= MaxSteps)
            {
                return Fail("Too many run steps.", TooManyStepsMessage());
            }

            sourceIndex++;
            steps.Add(new RunStepEntry(step, FileLineNumber: null, sourceIndex));
        }

        return RunStepLoadOutcome.Ok(steps);
    }

    private static async Task<int> ReadStepFileAsync(string path, List<RunStepEntry> steps, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
            || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("Run steps path must refer to a regular file.");
        }

        if (fileInfo.Length <= 0)
        {
            throw new InvalidDataException("Run steps file is empty.");
        }

        if (fileInfo.Length > MaxStepFileBytes)
        {
            throw new InvalidDataException($"Run steps file exceeds the maximum size of {MaxStepFileBytes.ToString(CultureInfo.InvariantCulture)} bytes.");
        }

        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var fileStreamDisposal = fileStream.ConfigureAwait(false);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
        var lineReader = new BoundedLineReader(reader, MaxStepLineChars);

        var lineIndex = 0;
        var sourceIndex = 0;
        while (await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineIndex++;
            if (lineIndex > MaxStepFileLines)
            {
                throw new InvalidDataException($"Run steps file exceeds the maximum of {MaxStepFileLines.ToString(CultureInfo.InvariantCulture)} lines.");
            }

            var trimmed = line.Trim();
            if (trimmed.Length is 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            sourceIndex++;
            if (steps.Count >= MaxSteps)
            {
                throw new InvalidDataException(TooManyStepsMessage());
            }

            steps.Add(new RunStepEntry(trimmed, lineIndex, sourceIndex));
        }

        return sourceIndex;
    }

    private static string TooManyStepsMessage() =>
        $"Run steps exceed the maximum of {MaxSteps.ToString(CultureInfo.InvariantCulture)} steps.";

    private static RunStepLoadOutcome Fail(string message, string error) =>
        RunStepLoadOutcome.Fail(new OperationResult
        {
            Success = false,
            Outcome = ExecutionOutcomeCode.FileError,
            Message = message,
            Errors = [error],
        });

    /// <summary>Reads lines and rejects one that is longer than the limit before it is fully buffered.</summary>
    private sealed class BoundedLineReader(StreamReader reader, int maxChars)
    {
        private readonly char[] _buffer = new char[4096];
        private readonly StringBuilder _line = new();
        private int _position;
        private int _length;

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = _line.Clear();
            while (true)
            {
                if (_position == _length)
                {
                    _length = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    _position = 0;
                    if (_length is 0)
                    {
                        return line.Length is 0 ? null : line.ToString();
                    }
                }

                var lineEnd = Array.IndexOf(_buffer, '\n', _position, _length - _position);
                var segmentLength = (lineEnd >= 0 ? lineEnd : _length) - _position;
                if (line.Length + segmentLength > maxChars)
                {
                    throw new InvalidDataException($"Run steps line exceeds the maximum of {maxChars.ToString(CultureInfo.InvariantCulture)} characters.");
                }

                _ = line.Append(_buffer, _position, segmentLength);
                _position += segmentLength;
                if (lineEnd < 0)
                {
                    continue;
                }

                _position++;
                if (line.Length > 0 && line[^1] is '\r')
                {
                    line.Length--;
                }

                return line.ToString();
            }
        }
    }
}
