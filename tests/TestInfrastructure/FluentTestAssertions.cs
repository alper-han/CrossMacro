namespace CrossMacro.TestInfrastructure;

internal static class FluentTestAssertions
{
    internal static async Task ThrowsWithMessageAsync<TException>(Func<Task> action, string expectedMessage)
        where TException : Exception
    {
        var assertions = await action.Should().ThrowAsync<TException>();
        _ = assertions.WithMessage(expectedMessage);
    }
}
