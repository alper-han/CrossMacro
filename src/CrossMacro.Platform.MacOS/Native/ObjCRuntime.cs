namespace CrossMacro.Platform.MacOS.Native;

/// <summary>
/// Objective-C runtime entry points shared by the AppKit/Foundation bridges.
/// Signature-specific <c>objc_msgSend</c> variants stay with their callers and import from <see cref="Library"/>.
/// </summary>
internal static partial class ObjCRuntime
{
    internal const string Library = "/usr/lib/libobjc.A.dylib";

    private const string AppKitLib = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string FoundationLib = "/System/Library/Frameworks/Foundation.framework/Foundation";

    private static readonly Lazy<bool> FrameworksAvailable = new(LoadFrameworks);
    private static readonly Lazy<IntPtr> AutoreleasePoolClass = new(() => GetClass("NSAutoreleasePool"));
    private static readonly Lazy<IntPtr> AllocSelector = new(() => RegisterSelector("alloc"));
    private static readonly Lazy<IntPtr> InitSelector = new(() => RegisterSelector("init"));
    private static readonly Lazy<IntPtr> ReleaseSelector = new(() => RegisterSelector("release"));
    private static readonly Lazy<IntPtr> DrainSelector = new(() => RegisterSelector("drain"));

    /// <summary>True on macOS when AppKit and Foundation load and the autorelease pool class resolves.</summary>
    internal static bool IsAvailable => FrameworksAvailable.Value && AutoreleasePoolClass.Value != IntPtr.Zero;

    /// <summary>Allocates an instance of <paramref name="cls"/>; the caller owns it and must <see cref="Release"/> it.</summary>
    internal static IntPtr Alloc(IntPtr cls) => SendIntPtr(cls, AllocSelector.Value);

    /// <summary>Initialises an allocated object, releasing it when <c>init</c> fails.</summary>
    internal static IntPtr Init(IntPtr allocated)
    {
        var initialized = SendIntPtr(allocated, InitSelector.Value);
        if (initialized == IntPtr.Zero)
        {
            Release(allocated);
        }

        return initialized;
    }

    internal static void Release(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            SendVoid(instance, ReleaseSelector.Value);
        }
    }

    /// <summary>Opens an autorelease pool that drains when disposed; an unavailable pool disposes as a no-op.</summary>
    internal static AutoreleasePool PushAutoreleasePool() =>
        new(Init(Alloc(AutoreleasePoolClass.Value)));

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr SendIntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr SendIntPtr(IntPtr receiver, IntPtr selector, nuint argument);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    internal static partial nuint SendNuint(IntPtr receiver, IntPtr selector);

    [LibraryImport(Library, EntryPoint = "objc_msgSend")]
    internal static partial void SendVoid(IntPtr receiver, IntPtr selector);

    private static bool LoadFrameworks() =>
        OperatingSystem.IsMacOS()
        && NativeLibrary.TryLoad(FoundationLib, out _)
        && NativeLibrary.TryLoad(AppKitLib, out _);

    private static void Drain(IntPtr pool)
    {
        if (pool != IntPtr.Zero)
        {
            SendVoid(pool, DrainSelector.Value);
        }
    }

    [LibraryImport(Library, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr GetClass(string name);

    [LibraryImport(Library, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr RegisterSelector(string name);

    internal readonly struct AutoreleasePool(IntPtr pool) : IDisposable
    {
        public void Dispose() => Drain(pool);
    }
}
