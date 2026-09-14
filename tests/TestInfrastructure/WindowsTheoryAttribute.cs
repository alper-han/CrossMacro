using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

internal sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute(
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
