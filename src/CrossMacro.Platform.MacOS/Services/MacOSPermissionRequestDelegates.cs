using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Services;

internal static class MacOSPermissionRequestDelegates
{
    internal static Func<bool> RequestListenEventAccess(IPermissionChecker permissionChecker)
    {
        return permissionChecker is IMacOSPermissionChecker macOSPermissionChecker
            ? macOSPermissionChecker.RequestListenEventAccess
            : () => false;
    }

    internal static Func<bool> RequestPostEventAccess(IPermissionChecker permissionChecker)
    {
        return permissionChecker is IMacOSPermissionChecker macOSPermissionChecker
            ? macOSPermissionChecker.RequestPostEventAccess
            : () => false;
    }
}
