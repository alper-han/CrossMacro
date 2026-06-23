using System;

namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenPixelColor(byte R, byte G, byte B)
{
    public const int HexLength = 6;

    public static ScreenPixelColor Parse(string value)
    {
        if (!TryParse(value, out var color))
        {
            throw new FormatException("Screen pixel colors must be exactly 6 hexadecimal RGB characters.");
        }

        return color;
    }

    public static bool TryParse(string? value, out ScreenPixelColor color)
    {
        if (value is null)
        {
            color = default;
            return false;
        }

        return TryParse(value.AsSpan(), out color);
    }

    public static bool TryParse(ReadOnlySpan<char> value, out ScreenPixelColor color)
    {
        if (value.Length != HexLength)
        {
            color = default;
            return false;
        }

        var redHigh = ReadHex(value[0]);
        var redLow = ReadHex(value[1]);
        var greenHigh = ReadHex(value[2]);
        var greenLow = ReadHex(value[3]);
        var blueHigh = ReadHex(value[4]);
        var blueLow = ReadHex(value[5]);

        if (redHigh < 0 || redLow < 0 || greenHigh < 0 || greenLow < 0 || blueHigh < 0 || blueLow < 0)
        {
            color = default;
            return false;
        }

        color = new ScreenPixelColor(
            (byte)((redHigh << 4) | redLow),
            (byte)((greenHigh << 4) | greenLow),
            (byte)((blueHigh << 4) | blueLow));
        return true;
    }

    public override string ToString() => string.Create(HexLength, this, static (span, color) =>
    {
        WriteByte(span[..2], color.R);
        WriteByte(span[2..4], color.G);
        WriteByte(span[4..6], color.B);
    });

    public bool IsWithinTolerance(ScreenPixelColor expected, int tolerance)
    {
        if (tolerance is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Screen pixel tolerance must be between 0 and 255.");
        }

        return Math.Abs(R - expected.R) <= tolerance
            && Math.Abs(G - expected.G) <= tolerance
            && Math.Abs(B - expected.B) <= tolerance;
    }

    private static int ReadHex(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'A' and <= 'F' => value - 'A' + 10,
        >= 'a' and <= 'f' => value - 'a' + 10,
        _ => -1
    };

    private static void WriteByte(Span<char> target, byte value)
    {
        target[0] = WriteHex(value >> 4);
        target[1] = WriteHex(value & 0x0F);
    }

    private static char WriteHex(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);
}
