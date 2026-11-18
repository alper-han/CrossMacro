using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = ConditionalSkipMessage.For("Linux");
        }
    }
}
