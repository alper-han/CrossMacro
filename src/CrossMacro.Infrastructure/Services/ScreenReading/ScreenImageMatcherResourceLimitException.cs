
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageMatcherResourceLimitException : InvalidOperationException
{
    public ScreenImageMatcherResourceLimitException()
        : base("Screen image matcher resource limit exceeded.")
    {
    }

    public ScreenImageMatcherResourceLimitException(string message)
        : base(message)
    {
    }

    public ScreenImageMatcherResourceLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ScreenImageMatcherResourceLimitException(long requestedWork, long maximumWork, string message)
        : base(message)
    {
        RequestedWork = requestedWork;
        MaximumWork = maximumWork;
    }

    public ScreenImageMatcherResourceLimitException(long requestedWork, long maximumWork, string message, Exception innerException)
        : base(message, innerException)
    {
        RequestedWork = requestedWork;
        MaximumWork = maximumWork;
    }

    public long RequestedWork { get; }

    public long MaximumWork { get; }

    internal bool IsPreparationLimit { get; init; }
}
