
namespace CrossMacro.TestInfrastructure;

public sealed class WindowsFactAttribute : ConditionalFactAttribute
{
    public WindowsFactAttribute() : base(OperatingSystem.IsWindows, "Windows")
    {
    }
}
