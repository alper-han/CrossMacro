using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Core.Logging;

namespace CrossMacro.Infrastructure.Services.TextExpansion
{
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
            _buffer.Append(c);
            if (_buffer.Length > MaxBufferLength)
            {
                _buffer.Remove(0, _buffer.Length - MaxBufferLength);
            }
            // Log.Debug("[TextBuffer] Content: {Buffer}", _buffer.ToString());
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
            _buffer.Clear();
        }

        public bool TryGetMatch(IEnumerable<Core.Models.TextExpansion> expansions, out Core.Models.TextExpansion? match)
        {
            match = null;
            if (_buffer.Length == 0) return false;

            string currentText = _buffer.ToString();
            
            // Look for triggered expansions
            
            var validExpansions = expansions.Where(e => e.IsEnabled && !string.IsNullOrEmpty(e.Trigger));

            foreach (var expansion in validExpansions)
            {
                if (currentText.EndsWith(expansion.Trigger))
                {
                    match = expansion;
                    return true;
                }
            }

            return false;
        }
    }
}
