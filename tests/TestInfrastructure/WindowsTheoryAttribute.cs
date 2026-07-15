using Xunit;

namespace CrossMacro.TestInfrastructure;

public sealed class WindowsTheoryAttribute : ConditionalTheoryAttribute
{
    public WindowsTheoryAttribute() : base(OperatingSystem.IsWindows, "Windows")
    {
    }
}
