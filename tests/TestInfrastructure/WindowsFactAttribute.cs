
namespace CrossMacro.TestInfrastructure;

internal sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = ConditionalSkipMessage.For("Windows");
        }
    }
}
