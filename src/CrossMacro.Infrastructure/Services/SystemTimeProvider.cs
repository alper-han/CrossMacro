
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Default implementation of ITimeProvider using system time
/// </summary>
public class SystemTimeProvider : ITimeProvider
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}
