
namespace CrossMacro.TestInfrastructure;

public sealed class MacOSFactAttribute : ConditionalFactAttribute
{
    public MacOSFactAttribute() : base(OperatingSystem.IsMacOS, "macOS")
    {
    }
}
