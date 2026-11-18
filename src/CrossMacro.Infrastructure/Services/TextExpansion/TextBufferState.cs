namespace CrossMacro.Infrastructure.Services.TextExpansion;

/// <summary>
/// The most recently typed characters. A trigger longer than <see cref="MaxBufferLength"/> can never match.
/// </summary>
public sealed class TextBufferState : ITextBufferState
{
    public const int MaxBufferLength = 256;

    private readonly char[] _buffer = new char[MaxBufferLength];
    private int _length;

    public void Append(char c)
    {
        if (_length == MaxBufferLength)
        {
            Array.Copy(_buffer, 1, _buffer, 0, MaxBufferLength - 1);
            _length--;
        }

        _buffer[_length++] = c;
    }

    public void Backspace()
    {
        if (_length > 0)
        {
            _length--;
        }
    }

    public void Clear()
    {
        _length = 0;
    }

    /// <summary>Finds the longest enabled trigger that the typed text ends with, comparing ordinally.</summary>
    public bool TryGetMatch(IEnumerable<TextExpansionEntry> expansions, out TextExpansionEntry? match)
    {
        ArgumentNullException.ThrowIfNull(expansions);
        match = null;
        var typed = _buffer.AsSpan(0, _length);
        foreach (var expansion in expansions)
        {
            if (!expansion.IsEnabled
                || string.IsNullOrEmpty(expansion.Trigger)
                || (match is not null && expansion.Trigger.Length <= match.Trigger.Length)
                || !typed.EndsWith(expansion.Trigger, StringComparison.Ordinal))
            {
                continue;
            }

            match = expansion;
        }

        return match is not null;
    }
}
