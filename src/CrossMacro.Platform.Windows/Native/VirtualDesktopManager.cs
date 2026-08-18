
namespace CrossMacro.Platform.Windows.Native;

internal sealed partial class VirtualDesktopManager : IDisposable
{
    private static readonly Guid Clsid = new(0xaa509086, 0x5ca9, 0x4c25, 0x8f, 0x95, 0x58, 0x9d, 0x3c, 0x07, 0xb4, 0x8a);
    private static readonly Guid Iid = new(0xa5cd92ff, 0x29be, 0x454c, 0x8d, 0x04, 0xd8, 0x28, 0x79, 0xfb, 0x3f, 0x1b);
    private const uint ClsctxAll = 0x17;
    private const uint CoinitApartmentThreaded = 0x2;
    private const int SOk = 0;
    private const int SFalse = 1;
    private const int RpcEChangedMode = unchecked((int)0x80010106);
    private readonly ComApartmentScope _apartment;
    private IntPtr _instance;
    private bool _disposed;

    private VirtualDesktopManager(IntPtr instance, ComApartmentScope apartment)
    {
        _instance = instance;
        _apartment = apartment;
    }

    public static VirtualDesktopManager Create()
    {
        var apartment = ComApartmentScope.Initialize();
        var clsid = Clsid;
        var iid = Iid;

        try
        {
            int hr = CoCreateInstance(ref clsid, IntPtr.Zero, ClsctxAll, ref iid, out var instance);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return new VirtualDesktopManager(instance, apartment);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            apartment.Dispose();
            throw;
        }
    }

    private delegate int VirtualDesktopManagerMethod(IntPtr instance, IntPtr hwnd, ref Guid desktopId);

    public int GetWindowDesktopId(IntPtr hwnd, out Guid desktopId)
    {
        desktopId = Guid.Empty;
        var function = GetMethodDelegate<VirtualDesktopManagerMethod>(4);
        return function(_instance, hwnd, ref desktopId);
    }

    public int MoveWindowToDesktop(IntPtr hwnd, ref Guid desktopId)
    {
        var function = GetMethodDelegate<VirtualDesktopManagerMethod>(5);
        return function(_instance, hwnd, ref desktopId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_instance != IntPtr.Zero)
        {
            _ = Marshal.Release(_instance);
            _instance = IntPtr.Zero;
        }

        _apartment.Dispose();
        _disposed = true;
    }

    private T GetMethodDelegate<T>(int slot) where T : Delegate
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IntPtr vtable = Marshal.ReadIntPtr(_instance);
        IntPtr methodPtr = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
    }

    private sealed class ComApartmentScope : IDisposable
    {
        private readonly int _threadId;
        private readonly bool _uninitialize;
        private bool _disposed;

        private ComApartmentScope(bool uninitialize)
        {
            _uninitialize = uninitialize;
            _threadId = Environment.CurrentManagedThreadId;
        }

        public static ComApartmentScope Initialize()
        {
            int hr = CoInitializeEx(IntPtr.Zero, CoinitApartmentThreaded);

            if (hr is SOk or SFalse)
            {
                return new ComApartmentScope(uninitialize: true);
            }

            if (hr == RpcEChangedMode)
            {
                return new ComApartmentScope(uninitialize: false);
            }

            Marshal.ThrowExceptionForHR(hr);
            return new ComApartmentScope(uninitialize: false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_uninitialize && Environment.CurrentManagedThreadId == _threadId)
            {
                CoUninitialize();
            }

            _disposed = true;
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);
}
