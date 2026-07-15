
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageMatcherResourceLimitException : InvalidOperationException
{
    public ScreenImageMatcherResourceLimitException(long requestedWork, long maximumWork, string message)
        : base(message)
    {
        RequestedWork = requestedWork;
        MaximumWork = maximumWork;
    }

    public long RequestedWork { get; }

    public long MaximumWork { get; }
}
