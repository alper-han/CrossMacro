using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Native.X11;
using CrossMacro.Platform.Linux.Native.UInput;
using Serilog;

namespace CrossMacro.Platform.Linux.Services
{
    /// <summary>
    /// Base class for X11 Input Capture implementations.
    /// Handles X server connection, XInput2 initialization, and the main event loop.
    /// </summary>
    public abstract class X11CaptureBase : IInputCapture, IDisposable
    {
        protected IntPtr _display;
        protected IntPtr _rootWindow;
        private Thread? _captureThread;
        protected volatile bool _isRunning;
        private bool _disposed;
        
        protected bool _captureMouse;
        protected bool _captureKeyboard;

        public abstract string ProviderName { get; }

        public bool IsSupported
        {
            get
            {
                try
                {
                    var dpy = X11Native.XOpenDisplay(null);
                    if (dpy == IntPtr.Zero) return false;
                    
                    int major = XInput2Consts.XINPUT2_MAJOR_VERSION;
                    int minor = XInput2Consts.XINPUT2_MINOR_VERSION;
                    int res = X11Native.XIQueryVersion(dpy, ref major, ref minor);
                    X11Native.XCloseDisplay(dpy);
                    
                    return res == 0; 
                }
                catch
                {
                    return false;
                }
            }
        }

        public event EventHandler<InputCaptureEventArgs>? InputReceived;
        public event EventHandler<string>? Error;

        public void Configure(bool captureMouse, bool captureKeyboard)
        {
            _captureMouse = captureMouse;
            _captureKeyboard = captureKeyboard;
        }

        public Task StartAsync(CancellationToken ct)
        {
            if (_isRunning)
            {
                return Task.CompletedTask;
            }

            _isRunning = true;
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = GetType().Name
            };
            _captureThread.Start();

            ct.Register(Stop);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void CaptureLoop()
        {
            try
            {
                _display = X11Native.XOpenDisplay(null);
                if (_display == IntPtr.Zero)
                {
                    Error?.Invoke(this, "Failed to open X Display");
                    return;
                }

                _rootWindow = X11Native.XDefaultRootWindow(_display);

                // Init XI2
                int major = XInput2Consts.XINPUT2_MAJOR_VERSION;
                int minor = XInput2Consts.XINPUT2_MINOR_VERSION;
                if (X11Native.XIQueryVersion(_display, ref major, ref minor) != 0)
                {
                    Error?.Invoke(this, "XInput2 extension not available");
                    return;
                }

                var maskBytes = new byte[4];

                if (_captureKeyboard)
                {
                    XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawKeyPress);
                    XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawKeyRelease);
                }

                if (_captureMouse)
                {
                    XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawButtonPress);
                    XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawButtonRelease);
                    XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawMotion);
                }

                IntPtr maskPtr = Marshal.AllocHGlobal(maskBytes.Length);
                try
                {
                    Marshal.Copy(maskBytes, 0, maskPtr, maskBytes.Length);

                    var mask = new XIEventMask
                    {
                        DeviceId = XInput2Consts.XIAllMasterDevices,
                        MaskLen = maskBytes.Length,
                        Mask = maskPtr
                    };

                    X11Native.XISelectEvents(_display, _rootWindow, ref mask, 1);
                    X11Native.XFlush(_display);
                }
                finally
                {
                    Marshal.FreeHGlobal(maskPtr);
                }
                
                OnCaptureStarted();

                IntPtr eventPtr = Marshal.AllocHGlobal(XInput2Consts.XEVENT_STRUCT_SIZE);
                try 
                {
                    while (_isRunning)
                    {
                        // Check if we have events pending
                        if (X11Native.XPending(_display) > 0)
                        {
                            X11Native.XNextEvent(_display, eventPtr);
                            var xEvent = Marshal.PtrToStructure<XEvent>(eventPtr);

                            if (xEvent.xcookie.type == XInput2Consts.GenericEvent && 
                                X11Native.XGetEventData(_display, eventPtr))
                            {
                                try
                                {
                                    var cookie = Marshal.PtrToStructure<XGenericEventCookie>(eventPtr);
                                    ProcessGenericEvent(cookie);
                                }
                                finally
                                {
                                    X11Native.XFreeEventData(_display, eventPtr);
                                }
                            }
                        }
                        else
                        {
                            OnLoopIdle();
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(eventPtr);
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex.Message);
            }
            finally
            {
                if (_display != IntPtr.Zero)
                {
                    X11Native.XCloseDisplay(_display);
                    _display = IntPtr.Zero;
                }
            }
        }
        
        /// <summary>
        /// Called after X11 connection and selection are established, before the loop starts.
        /// </summary>
        protected virtual void OnCaptureStarted() { }

        /// <summary>
        /// Called when no X events are pending. Default implementation sleeps 1ms.
        /// </summary>
        protected virtual void OnLoopIdle()
        {
            Thread.Sleep(1);
        }

        /// <summary>
        /// Called before processing a Key/Button event. subclasses can override to flush pending motion.
        /// </summary>
        protected virtual void FlushPendingMotion() { }

        /// <summary>
        /// Handles motion events.
        /// </summary>
        protected abstract void ProcessMotion(XGenericEventCookie cookie);

        private void ProcessGenericEvent(XGenericEventCookie cookie)
        {
            // Motion
            if (cookie.evtype == XInput2Consts.XI_RawMotion)
            {
                ProcessMotion(cookie);
                return;
            }

            var rawEvent = Marshal.PtrToStructure<XIRawEvent>(cookie.data);

            // Keyboard
            if (cookie.evtype == XInput2Consts.XI_RawKeyPress || cookie.evtype == XInput2Consts.XI_RawKeyRelease)
            {
                // Hook for subclasses to flush motion before key 
                FlushPendingMotion();

               int code = rawEvent.detail - LinuxConstants.X11ToLinuxKeycodeOffset;
               int value = (cookie.evtype == XInput2Consts.XI_RawKeyPress) ? 1 : 0;
               
               var args = new InputCaptureEventArgs
               {
                   Type = InputEventType.Key,
                   Code = (ushort)code,
                   Value = value,
                   Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                   DeviceName = ProviderName
               };
               InputReceived?.Invoke(this, args);
               return;
            }
            
            // Mouse Buttons
            if (cookie.evtype == XInput2Consts.XI_RawButtonPress || cookie.evtype == XInput2Consts.XI_RawButtonRelease)
            {
                // Hook for subclasses to flush motion before click
                FlushPendingMotion();

                int code = rawEvent.detail;
                int value = (cookie.evtype == XInput2Consts.XI_RawButtonPress) ? 1 : 0;
                InputEventType type = InputEventType.MouseButton;

                // Handle Scroll (buttons 4-7)
                if (code >= XInput2Consts.X11_SCROLL_UP && code <= XInput2Consts.X11_SCROLL_RIGHT)
                {
                    if (value == 0) return;
                    type = InputEventType.MouseScroll;
                    
                    if (code == XInput2Consts.X11_SCROLL_UP || code == XInput2Consts.X11_SCROLL_DOWN)
                    {
                        // Vertical
                        value = (code == XInput2Consts.X11_SCROLL_UP) 
                            ? XInput2Consts.SCROLL_DELTA 
                            : -XInput2Consts.SCROLL_DELTA;
                        code = XInput2Consts.SCROLL_AXIS_VERTICAL;
                    }
                    else
                    {
                        // Horizontal (Left=6, Right=7)
                        value = (code == XInput2Consts.X11_SCROLL_RIGHT) 
                            ? XInput2Consts.SCROLL_DELTA 
                            : -XInput2Consts.SCROLL_DELTA;
                        code = XInput2Consts.SCROLL_AXIS_HORIZONTAL;
                    }
                }
                else
                {
                    code = MapX11ButtonToLinux(code);
                }

                var args = new InputCaptureEventArgs
                {
                    Type = type,
                    Code = (ushort)code,
                    Value = value,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DeviceName = ProviderName
                };
                InputReceived?.Invoke(this, args);
            }
        }

        private int MapX11ButtonToLinux(int x11Btn)
        {
            // Mapping based on linux/input-event-codes.h
            return x11Btn switch
            {
                1 => UInputNative.BTN_LEFT,
                2 => UInputNative.BTN_MIDDLE, 
                3 => UInputNative.BTN_RIGHT,
                8 => UInputNative.BTN_SIDE, 
                9 => UInputNative.BTN_EXTRA, 
                _ => x11Btn // Unknown
            };
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
        
        // Helper for subclasses to emit events
        protected void OnInputReceived(InputCaptureEventArgs args)
        {
            InputReceived?.Invoke(this, args);
        }
    }
}
