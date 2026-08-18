
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public class TextBufferState : ITextBufferState
{
    private readonly StringBuilder _buffer;
    private const int MaxBufferLength = 50;

    public TextBufferState()
    {
        _buffer = new StringBuilder();
    }

    public void Append(char c)
    {
        _ = _buffer.Append(c);
        if (_buffer.Length > MaxBufferLength)
        {
            _ = _buffer.Remove(0, _buffer.Length - MaxBufferLength);
        }
    }

    public void Backspace()
    {
        if (_buffer.Length > 0)
        {
            _buffer.Length--;
        }
    }

    public void Clear()
    {
        _ = _buffer.Clear();
    }

    public bool TryGetMatch(IEnumerable<Core.Models.TextExpansionEntry> expansions, out Core.Models.TextExpansionEntry? match)
    {
        match = null;
        if (_buffer.Length is 0)
        {
            return false;
        }

        string currentText = _buffer.ToString();

        // Look for triggered expansions

        match = expansions.FirstOrDefault(e => e.IsEnabled && !string.IsNullOrEmpty(e.Trigger)
            && currentText.EndsWith(e.Trigger, StringComparison.CurrentCulture));
        return match is not null;
    }
}
