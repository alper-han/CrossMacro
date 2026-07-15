using Xunit;

namespace CrossMacro.TestInfrastructure;

public sealed class LinuxTheoryAttribute : ConditionalTheoryAttribute
{
    public LinuxTheoryAttribute() : base(OperatingSystem.IsLinux, "Linux")
    {
    }
}
