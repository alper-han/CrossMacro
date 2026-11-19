
namespace CrossMacro.Platform.Windows.Native;

internal static partial class Winmm
{
    [LibraryImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    internal static partial uint TimeBeginPeriod(uint periodMilliseconds);
}
