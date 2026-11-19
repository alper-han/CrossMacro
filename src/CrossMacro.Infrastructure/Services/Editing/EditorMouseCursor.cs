namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Tracks the cursor position that script <c>move</c> steps establish while steps are restored in order.</summary>
internal sealed class EditorMouseCursor
{
    private MouseCoordinateMode? _mode;
    private MouseCoordinateSpace? _coordinateSpace;
    private bool _hasAbsolutePosition;
    private string _absoluteX = "0";
    private string _absoluteY = "0";

    internal void RecordMove(MouseCoordinateMode mode, MouseCoordinateSpace coordinateSpace, string x, string y)
    {
        _mode = mode;
        _coordinateSpace = coordinateSpace;
        _hasAbsolutePosition = mode is MouseCoordinateMode.Absolute;
        if (_hasAbsolutePosition)
        {
            _absoluteX = x;
            _absoluteY = y;
        }
    }

    /// <summary>
    /// Returns the position a bare button step acts at. False when the step targets the current cursor
    /// position, either explicitly or because no move established a position.
    /// </summary>
    internal bool TryGetPosition(bool isCurrentPositionExplicit, out CursorPosition position)
    {
        position = default;
        if (isCurrentPositionExplicit)
        {
            return false;
        }

        if (_mode is MouseCoordinateMode.Absolute && _hasAbsolutePosition)
        {
            position = new CursorPosition(IsAbsolute: true, MouseCoordinateSpace.LogicalDesktop, _absoluteX, _absoluteY);
            return true;
        }

        if (_mode is MouseCoordinateMode.Relative)
        {
            position = new CursorPosition(IsAbsolute: false, _coordinateSpace ?? MouseCoordinateSpace.LogicalDesktop, "0", "0");
            return true;
        }

        return false;
    }

    internal readonly record struct CursorPosition(bool IsAbsolute, MouseCoordinateSpace CoordinateSpace, string X, string Y);
}
