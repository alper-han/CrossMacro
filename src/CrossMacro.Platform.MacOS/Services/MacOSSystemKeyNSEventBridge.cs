using System;
using System.Runtime.InteropServices;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

internal static class MacOSSystemKeyNSEventBridge
{
    private const string AppKitLib = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string FoundationLib = "/System/Library/Frameworks/Foundation.framework/Foundation";
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

    private static readonly Lazy<bool> BridgeAvailable = new(IsBridgeAvailableCore);
    private static readonly Lazy<IntPtr> NSEventClass = new(() => objc_getClass("NSEvent"));
    private static readonly Lazy<IntPtr> NSAutoreleasePoolClass = new(() => objc_getClass("NSAutoreleasePool"));
    private static readonly Lazy<IntPtr> OtherEventSelector = new(() => sel_registerName("otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:"));
    private static readonly Lazy<IntPtr> CGEventSelector = new(() => sel_registerName("CGEvent"));
    private static readonly Lazy<IntPtr> AllocSelector = new(() => sel_registerName("alloc"));
    private static readonly Lazy<IntPtr> InitSelector = new(() => sel_registerName("init"));
    private static readonly Lazy<IntPtr> DrainSelector = new(() => sel_registerName("drain"));

    internal static bool IsAvailable => BridgeAvailable.Value;

    internal static bool TryCreateSystemDefinedCGEvent(
        MacOSSystemKeyEventPayload payload,
        out IntPtr eventRef)
    {
        eventRef = IntPtr.Zero;
        if (!IsAvailable)
        {
            return false;
        }

        var autoreleasePool = IntPtr.Zero;
        try
        {
            autoreleasePool = TryCreateAutoreleasePool();
            var nsEvent = objc_msgSend_otherEventWithType(
                NSEventClass.Value,
                OtherEventSelector.Value,
                (nuint)payload.EventType,
                new NSPoint(),
                (nuint)payload.Flags,
                timestamp: 0,
                windowNumber: 0,
                context: IntPtr.Zero,
                subtype: (short)payload.Subtype,
                data1: (nint)payload.Data1,
                data2: (nint)payload.Data2);

            if (nsEvent == IntPtr.Zero)
            {
                return false;
            }

            var cgEvent = objc_msgSend_IntPtr(nsEvent, CGEventSelector.Value);
            if (cgEvent == IntPtr.Zero)
            {
                return false;
            }

            eventRef = CoreFoundation.CFRetain(cgEvent);
            return eventRef != IntPtr.Zero;
        }
        catch (DllNotFoundException)
        {
            eventRef = IntPtr.Zero;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            eventRef = IntPtr.Zero;
            return false;
        }
        finally
        {
            DrainAutoreleasePool(autoreleasePool);
        }
    }

    private static bool IsBridgeAvailableCore()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            return NativeLibrary.TryLoad(FoundationLib, out _)
                && NativeLibrary.TryLoad(AppKitLib, out _)
                && NSEventClass.Value != IntPtr.Zero
                && NSAutoreleasePoolClass.Value != IntPtr.Zero;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static IntPtr TryCreateAutoreleasePool()
    {
        var poolClass = NSAutoreleasePoolClass.Value;
        if (poolClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var allocatedPool = objc_msgSend_IntPtr(poolClass, AllocSelector.Value);
        return allocatedPool == IntPtr.Zero
            ? IntPtr.Zero
            : objc_msgSend_IntPtr(allocatedPool, InitSelector.Value);
    }

    private static void DrainAutoreleasePool(IntPtr autoreleasePool)
    {
        if (autoreleasePool != IntPtr.Zero)
        {
            objc_msgSend_IntPtr(autoreleasePool, DrainSelector.Value);
        }
    }

    [DllImport(ObjCLib, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCLib, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_otherEventWithType(
        IntPtr receiver,
        IntPtr selector,
        nuint type,
        NSPoint location,
        nuint modifierFlags,
        double timestamp,
        nint windowNumber,
        IntPtr context,
        short subtype,
        nint data1,
        nint data2);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NSPoint
    {
        private readonly double _x;
        private readonly double _y;
    }
}
