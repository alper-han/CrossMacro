using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
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
