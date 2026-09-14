using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

internal sealed class MacOSFactAttribute : FactAttribute
{
    public MacOSFactAttribute(
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
