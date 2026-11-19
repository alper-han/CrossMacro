using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11;

/// <summary>
/// Ignores X protocol errors for the duration of a scope. Xlib's default error handler prints the
/// error and exits the process, so a request the server may legitimately refuse has to run inside a
/// trap; the refused request then reports failure through its return value.
/// </summary>
/// <remarks>
/// The Xlib error handler is process-wide. Scopes are serialized, and the handler that was installed
/// before (the default one, or the UI toolkit's) is restored when the scope ends.
/// </remarks>
internal static class X11ErrorTrap
{
    private static readonly Lock Gate = new();

    // Rooted for the lifetime of the process: Xlib keeps the function pointer.
    private static readonly ErrorHandler IgnoreErrorHandler = IgnoreError;
    private static readonly IntPtr IgnoreErrorPointer = Marshal.GetFunctionPointerForDelegate(IgnoreErrorHandler);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ErrorHandler(IntPtr display, IntPtr errorEvent);

    public static Scope Enter()
    {
        Gate.Enter();
        try
        {
            return new Scope(X11Native.XSetErrorHandler(IgnoreErrorPointer));
        }
        catch
        {
            Gate.Exit();
            throw;
        }
    }

    private static int IgnoreError(IntPtr display, IntPtr errorEvent) => 0;

    internal readonly ref struct Scope(IntPtr previousHandler)
    {
        private readonly IntPtr _previousHandler = previousHandler;

        public void Dispose()
        {
            try
            {
                _ = X11Native.XSetErrorHandler(_previousHandler);
            }
            finally
            {
                Gate.Exit();
            }
        }
    }
}
