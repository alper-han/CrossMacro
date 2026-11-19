using System.Runtime.CompilerServices;

namespace CrossMacro.Platform.Linux.Tests;

/// <summary>
/// Opt-in facts that drive a real desktop session. They run only when the named environment
/// variable is <c>1</c> and the session matches: a desktop containing <see cref="RequiredDesktop"/>
/// when it is set, otherwise any Wayland session.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class LiveSessionFactAttribute : FactAttribute
{
    public LiveSessionFactAttribute(
        string optInVariable,
        string? requiredDesktop = null,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        OptInVariable = optInVariable;
        RequiredDesktop = requiredDesktop;
        Skip = ResolveSkip();
    }

    public string OptInVariable { get; }

    public string? RequiredDesktop { get; }

    private string? ResolveSkip()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            return ConditionalSkipMessage.For($"{OptInVariable}=1");
        }

        if (RequiredDesktop is not null)
        {
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            return desktop?.Contains(RequiredDesktop, StringComparison.OrdinalIgnoreCase) is true
                ? null
                : ConditionalSkipMessage.For($"a {RequiredDesktop} session");
        }

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var isWayland = !string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")));
        return isWayland ? null : ConditionalSkipMessage.For("a Wayland session");
    }
}
