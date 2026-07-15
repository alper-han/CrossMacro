using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Native;

internal sealed unsafe class VirtualDesktopManager : IDisposable
{
    private static readonly Guid Clsid = new("aa509086-5ca9-4c25-8f95-589d3c07b48a");
    private static readonly Guid Iid = new("a5cd92ff-29be-454c-8d04-d82879fb3f1b");
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
        catch
        {
            apartment.Dispose();
            throw;
        }
    }

    public int GetWindowDesktopId(IntPtr hwnd, out Guid desktopId)
    {
        desktopId = default;
        var function = GetGuidMethod(4);

        fixed (Guid* desktopIdPtr = &desktopId)
        {
            return function(_instance, hwnd, desktopIdPtr);
        }
    }

    public int MoveWindowToDesktop(IntPtr hwnd, ref Guid desktopId)
    {
        var function = GetGuidMethod(5);

        fixed (Guid* desktopIdPtr = &desktopId)
        {
            return function(_instance, hwnd, desktopIdPtr);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_instance != IntPtr.Zero)
        {
            Marshal.Release(_instance);
            _instance = IntPtr.Zero;
        }

        _apartment.Dispose();
        _disposed = true;
    }

    private delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, int> GetGuidMethod(int slot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var vtable = *(IntPtr**)_instance;
        return (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, int>)vtable[slot];
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

    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);
}
