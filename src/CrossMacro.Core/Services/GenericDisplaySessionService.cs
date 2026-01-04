namespace CrossMacro.Core.Services
{
    /// <summary>
    /// Generic implementation of IDisplaySessionService for platforms where session checks are not required (Windows, macOS).
    /// </summary>
    public class GenericDisplaySessionService : IDisplaySessionService
    {
        public bool IsSessionSupported(out string reason)
        {
            reason = string.Empty;
            return true;
        }
    }
}
