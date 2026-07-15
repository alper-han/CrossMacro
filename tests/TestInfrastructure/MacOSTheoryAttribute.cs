
namespace CrossMacro.TestInfrastructure;

public sealed class MacOSTheoryAttribute : ConditionalTheoryAttribute
{
    public MacOSTheoryAttribute() : base(OperatingSystem.IsMacOS, "macOS")
    {
    }
}
