namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Reads lines from a <see cref="StreamReader"/> and rejects any line longer than the configured limit
/// before it is fully buffered.
/// </summary>
internal sealed class BoundedLineReader(StreamReader reader, int maxChars)
{
    private readonly StreamReader _reader = reader;
    private readonly int _maxChars = maxChars;
    private readonly char[] _buffer = new char[4096];
    private int _bufferPosition;
    private int _bufferLength;

    public async Task<string?> ReadLineAsync()
    {
        var builder = new StringBuilder();
        while (true)
        {
            if (_bufferPosition == _bufferLength)
            {
                _bufferLength = await _reader.ReadAsync(_buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                _bufferPosition = 0;
                if (_bufferLength is 0)
                {
                    return builder.Length is 0 ? null : builder.ToString();
                }
            }

            var lineEnd = Array.IndexOf(_buffer, '\n', _bufferPosition, _bufferLength - _bufferPosition);
            var segmentEnd = lineEnd >= 0 ? lineEnd : _bufferLength;
            var segmentLength = segmentEnd - _bufferPosition;
            if (builder.Length + segmentLength > _maxChars)
            {
                throw new InvalidDataException($"Macro line exceeds the maximum of {_maxChars.ToString(CultureInfo.InvariantCulture)} characters.");
            }

            _ = builder.Append(_buffer, _bufferPosition, segmentLength);
            _bufferPosition = segmentEnd;
            if (lineEnd < 0)
            {
                continue;
            }

            _bufferPosition++;
            if (builder.Length > 0 && builder[^1] == '\r')
            {
                builder.Length--;
            }

            return builder.ToString();
        }
    }
}
