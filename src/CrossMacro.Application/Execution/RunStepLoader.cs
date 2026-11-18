using System.Globalization;
using System.Text;

namespace CrossMacro.Application.Execution;

internal static class RunStepLoader
{
    private const long MaxStepFileBytes = 8L * 1024 * 1024;
    private const int MaxStepLineChars = 64 * 1024;
    private const int MaxStepFileLines = 200_000;
    private const int MaxSteps = 100_000;

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

        var lineIndex = 0;
        var sourceIndex = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineIndex++;
            if (lineIndex > MaxStepFileLines)
            {
                throw new InvalidDataException($"Run steps file exceeds the maximum of {MaxStepFileLines.ToString(CultureInfo.InvariantCulture)} lines.");
            }

            if (line.Length > MaxStepLineChars)
            {
                throw new InvalidDataException($"Run steps line {lineIndex.ToString(CultureInfo.InvariantCulture)} exceeds the maximum of {MaxStepLineChars.ToString(CultureInfo.InvariantCulture)} characters.");
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
}
