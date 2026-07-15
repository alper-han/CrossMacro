using Xunit;

namespace CrossMacro.TestInfrastructure;

public sealed class NonMacOSFactAttribute : ConditionalFactAttribute
{
    public NonMacOSFactAttribute() : base(() => !OperatingSystem.IsMacOS(), "non-macOS environment")
    {
    }
}
