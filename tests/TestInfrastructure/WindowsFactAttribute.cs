using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

internal sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = ConditionalSkipMessage.For("Windows");
        }
    }
}
