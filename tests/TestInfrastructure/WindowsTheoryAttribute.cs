
namespace CrossMacro.TestInfrastructure;

internal sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = ConditionalSkipMessage.For("Windows");
        }
    }
}
