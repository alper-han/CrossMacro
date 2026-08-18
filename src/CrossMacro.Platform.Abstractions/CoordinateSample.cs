namespace CrossMacro.Platform.Abstractions;

public readonly record struct CoordinateSample
{
    private CoordinateSample(bool hasValue, int x, int y)
    {
        HasValue = hasValue;
        X = x;
        Y = y;
    }

    public bool HasValue { get; }

    public int X { get; }

    public int Y { get; }

    public static CoordinateSample None => default;

    public static CoordinateSample Create(int x, int y) => new(hasValue: true, x: x, y: y);
}
