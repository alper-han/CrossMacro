
namespace CrossMacro.Platform.MacOS.Native;

internal static partial class IOKit
{
    private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    [LibraryImport(IOKitLib)]
    private static partial IOHIDAccessType IOHIDCheckAccess(IOHIDRequestType requestType);

    [LibraryImport(IOKitLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool IOHIDRequestAccess(IOHIDRequestType requestType);

    public static bool CheckListenEventAccess()
    {
        return IOHIDCheckAccess(IOHIDRequestType.ListenEvent) is IOHIDAccessType.Granted;
    }

    public static bool RequestListenEventAccess()
    {
        return IOHIDRequestAccess(IOHIDRequestType.ListenEvent);
    }

    public enum IOHIDRequestType : uint
    {
        PostEvent = 0,
        ListenEvent = 1,
    }

    public enum IOHIDAccessType : uint
    {
        Granted = 0,
        Denied = 1,
        Unknown = 2,
    }
}
