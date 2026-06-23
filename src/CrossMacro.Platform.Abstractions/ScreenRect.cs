using System;

namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenRect
{
    public ScreenRect(int x, int y, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Screen rectangles require a positive width.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Screen rectangles require a positive height.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Right => checked(X + Width);

    public int Bottom => checked(Y + Height);

    public bool Contains(ScreenPoint point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    public bool Contains(ScreenRect rectangle) =>
        rectangle.X >= X && rectangle.Right <= Right && rectangle.Y >= Y && rectangle.Bottom <= Bottom;
}
