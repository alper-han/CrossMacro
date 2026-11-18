namespace CrossMacro.TestInfrastructure;

/// <summary>Upper bounds for awaited test signals. Generous enough for loaded CI runners; a healthy test never waits this long.</summary>
public static class TestTimeouts
{
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(15);
}
