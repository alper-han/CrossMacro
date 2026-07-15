using Xunit;

namespace CrossMacro.TestInfrastructure;

public sealed class LinuxFactAttribute : ConditionalFactAttribute
{
    public LinuxFactAttribute() : base(OperatingSystem.IsLinux, "Linux")
    {
    }
}
