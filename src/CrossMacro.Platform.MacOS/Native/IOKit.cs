using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class IOKit
{
    private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    [DllImport(IOKitLib)]
    private static extern IOHIDAccessType IOHIDCheckAccess(IOHIDRequestType requestType);

    [DllImport(IOKitLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool IOHIDRequestAccess(IOHIDRequestType requestType);

    public static bool CheckListenEventAccess()
    {
        return IOHIDCheckAccess(IOHIDRequestType.ListenEvent) == IOHIDAccessType.Granted;
    }

    public static bool RequestListenEventAccess()
    {
        return IOHIDRequestAccess(IOHIDRequestType.ListenEvent);
    }

    public enum IOHIDRequestType : uint
    {
        PostEvent = 0,
        ListenEvent = 1
    }

    public enum IOHIDAccessType : uint
    {
        Granted = 0,
        Denied = 1,
        Unknown = 2
    }
}
