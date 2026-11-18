
namespace CrossMacro.Platform.MacOS.Native;

internal static partial class IOKit
{
    private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    [LibraryImport(IOKitLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool IOHIDRequestAccess(IOHIDRequestType requestType);

    public enum IOHIDRequestType : uint
    {
        ListenEvent = 1,
    }
}
