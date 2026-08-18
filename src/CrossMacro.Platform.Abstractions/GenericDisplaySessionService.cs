
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// IDisplaySessionService for platforms where session checks are not required (Windows, macOS).
/// </summary>
public sealed class GenericDisplaySessionService : IDisplaySessionService
{
    public bool IsSessionSupported(out string reason)
    {
        reason = string.Empty;
        return true;
    }
}
