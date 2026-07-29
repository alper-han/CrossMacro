namespace CrossMacro.TestInfrastructure;

internal static partial class TestAssertions
{
    internal static void Throws<TException>(Action testCode)
        where TException : Exception
    {
        _ = Assert.Throws<TException>(testCode);
    }

    internal static async Task ThrowsAsync<TException>(Func<Task> testCode)
        where TException : Exception
    {
        _ = await Assert.ThrowsAsync<TException>(testCode);
    }

    internal static async Task ThrowsAnyAsync<TException>(Func<Task> testCode)
        where TException : Exception
    {
        _ = await Assert.ThrowsAnyAsync<TException>(testCode);
    }

    internal static void IsType<T>(object? value)
    {
        _ = Assert.IsType<T>(value);
    }

    internal static void Verify(Action verification)
    {
        verification();
    }

    internal static void VerifyTask(Func<Task> verification)
    {
        _ = verification();
    }
}
