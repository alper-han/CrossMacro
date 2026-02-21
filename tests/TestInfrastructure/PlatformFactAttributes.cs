using System;
using Xunit;

namespace CrossMacro.TestInfrastructure;

public abstract class ConditionalFactAttribute : FactAttribute
{
    protected ConditionalFactAttribute(Func<bool> predicate, string requiredEnvironment)
    {
        if (!predicate())
        {
            Skip = $"Requires {requiredEnvironment}.";
        }
    }
}

public abstract class ConditionalTheoryAttribute : TheoryAttribute
{
    protected ConditionalTheoryAttribute(Func<bool> predicate, string requiredEnvironment)
    {
        if (!predicate())
        {
            Skip = $"Requires {requiredEnvironment}.";
        }
    }
}

public sealed class LinuxFactAttribute : ConditionalFactAttribute
{
    public LinuxFactAttribute() : base(OperatingSystem.IsLinux, "Linux")
    {
    }
}

public sealed class LinuxTheoryAttribute : ConditionalTheoryAttribute
{
    public LinuxTheoryAttribute() : base(OperatingSystem.IsLinux, "Linux")
    {
    }
}

public sealed class LinuxIntegrationFactAttribute : ConditionalFactAttribute
{
    public LinuxIntegrationFactAttribute()
        : base(
            () => OperatingSystem.IsLinux() &&
                  string.Equals(
                      Environment.GetEnvironmentVariable("CROSSMACRO_DAEMON_INTEGRATION_TESTS"),
                      "1",
                      StringComparison.Ordinal),
            "Linux + CROSSMACRO_DAEMON_INTEGRATION_TESTS=1")
    {
    }
}

public sealed class MacOSFactAttribute : ConditionalFactAttribute
{
    public MacOSFactAttribute() : base(OperatingSystem.IsMacOS, "macOS")
    {
    }
}

public sealed class MacOSTheoryAttribute : ConditionalTheoryAttribute
{
    public MacOSTheoryAttribute() : base(OperatingSystem.IsMacOS, "macOS")
    {
    }
}

public sealed class WindowsFactAttribute : ConditionalFactAttribute
{
    public WindowsFactAttribute() : base(OperatingSystem.IsWindows, "Windows")
    {
    }
}

public sealed class WindowsTheoryAttribute : ConditionalTheoryAttribute
{
    public WindowsTheoryAttribute() : base(OperatingSystem.IsWindows, "Windows")
    {
    }
}

public sealed class NonMacOSFactAttribute : ConditionalFactAttribute
{
    public NonMacOSFactAttribute() : base(() => !OperatingSystem.IsMacOS(), "non-macOS environment")
    {
    }
}
