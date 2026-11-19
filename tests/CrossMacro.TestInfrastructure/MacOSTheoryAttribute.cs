using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MacOSTheoryAttribute : TheoryAttribute
{
    public MacOSTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = ConditionalSkipMessage.For("macOS");
        }
    }
}
