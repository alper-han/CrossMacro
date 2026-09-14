using System.Runtime.CompilerServices;

namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;

internal sealed class KWinScreenShotRuntimeFactAttribute : FactAttribute
{
    public KWinScreenShotRuntimeFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsLinux() || !HasLibX11())
        {
            Skip = "Requires Linux with libX11.so.6 for native KWin registrar runtime resolution.";
        }
    }

    private static bool HasLibX11()
    {
        if (!NativeLibrary.TryLoad("libX11.so.6", out var handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }
}
