using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

internal static class RunStepLoader
{
    private const long MaxStepFileBytes = 16L * 1024 * 1024;
    private const int MaxStepLineChars = 256 * 1024;
    private const int MaxStepFileLines = 100_000;
    private const int MaxSteps = 100_000;

    public static async Task<RunStepLoadOutcome> LoadAsync(RunExecutionRequest request, CancellationToken cancellationToken)
    {
        var steps = new List<RunStepEntry>();
        var sourceIndex = 0;
        if (!string.IsNullOrWhiteSpace(request.StepFilePath))
        {
            if (!File.Exists(request.StepFilePath))
            {
                return RunStepLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.FileError,
                    Message = "Run steps file not found.",
                    Errors = [$"File does not exist: {request.StepFilePath}"],
                });
            }

            try
            {
                var fileInfo = new FileInfo(request.StepFilePath);
                if ((fileInfo.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    throw new InvalidDataException("Run steps path must refer to a regular file.");
                }

                if (fileInfo.Length <= 0 || fileInfo.Length > MaxStepFileBytes)
                {
                    throw new InvalidDataException(fileInfo.Length <= 0
                        ? "Run steps file is empty."
                        : $"Run steps file exceeds the maximum size of {MaxStepFileBytes} bytes.");
                }

                await using var fileStream = new FileStream(request.StepFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
                var lineReader = new BoundedLineReader(reader, MaxStepLineChars);
                var lineIndex = 0;
                while (await lineReader.ReadLineAsync(cancellationToken) is { } line)
                {
                    lineIndex++;
                    if (lineIndex > MaxStepFileLines)
                    {
                        throw new InvalidDataException($"Run steps file exceeds the maximum of {MaxStepFileLines} lines.");
                    }

                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    sourceIndex++;
                    if (steps.Count >= MaxSteps)
                    {
                        throw new InvalidDataException($"Run steps exceed the maximum of {MaxSteps} steps.");
                    }

                    steps.Add(new RunStepEntry(trimmed, lineIndex, sourceIndex));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return RunStepLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.FileError,
                    Message = "Failed to read run steps file.",
                    Errors = [ex.Message],
                });
            }
        }

        foreach (var step in request.Steps)
        {
            if (steps.Count >= MaxSteps)
            {
                return RunStepLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.FileError,
                    Message = "Too many run steps.",
                    Errors = [$"Run steps exceed the maximum of {MaxSteps} steps."],
                });
            }

            sourceIndex++;
            steps.Add(new RunStepEntry(step, FileLineNumber: null, sourceIndex));
        }

        return RunStepLoadOutcome.Ok(steps);
    }

    private sealed class BoundedLineReader
    {
        private readonly StreamReader _reader;
        private readonly int _maxChars;
        private readonly char[] _buffer = new char[1];

        public BoundedLineReader(StreamReader reader, int maxChars)
        {
            _reader = reader;
            _maxChars = maxChars;
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            while (await _reader.ReadAsync(_buffer.AsMemory(0, 1), cancellationToken) > 0)
            {
                if (_buffer[0] == '\n')
                {
                    if (builder.Length > 0 && builder[^1] == '\r')
                    {
                        builder.Length--;
                    }

                    return builder.ToString();
                }

                if (builder.Length >= _maxChars)
                {
                    throw new InvalidDataException($"Run steps line exceeds the maximum of {_maxChars} characters.");
                }

                builder.Append(_buffer[0]);
            }

            return builder.Length is 0 ? null : builder.ToString();
        }
    }
}
