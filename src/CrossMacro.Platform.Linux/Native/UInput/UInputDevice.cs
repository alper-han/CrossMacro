using System;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Linux.Native.Evdev;
using Serilog;

namespace CrossMacro.Platform.Linux.Native.UInput
{
    public class UInputDevice : IDisposable
    {
        private int _fd;
        private bool _disposed;
        private readonly int _width;
        private readonly int _height;

        public UInputDevice(int width = 0, int height = 0)
        {
            _fd = -1;
            _width = width;
            _height = height;
        }

        public void CreateVirtualInputDevice()
        {
            Log.Information("[UInputDevice] Creating virtual input device (Mouse + Keyboard, Resolution: {Width}x{Height})...", _width, _height);
            
            // Try opening /dev/uinput
            _fd = UInputNative.open("/dev/uinput", UInputNative.O_WRONLY | UInputNative.O_NONBLOCK);
            if (_fd < 0)
            {
                // Try alternative path
                _fd = UInputNative.open("/dev/input/uinput", UInputNative.O_WRONLY | UInputNative.O_NONBLOCK);
            }

            if (_fd < 0)
            {
                var errno = Marshal.GetLastWin32Error();
                Log.Error("[UInputDevice] Failed to open /dev/uinput. Errno: {Errno}", errno);
                throw new IOException($"Cannot open /dev/uinput (Errno: {errno}). Check permissions (sudo chmod +0666 /dev/uinput).");
            }

            Log.Debug("[UInputDevice] Opened /dev/uinput with fd: {Fd}", _fd);

            try 
            {
                // Enable mouse button events (always needed)
                EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_KEY);
                EnableBit(UInputNative.UI_SET_KEYBIT, UInputNative.BTN_LEFT);
                EnableBit(UInputNative.UI_SET_KEYBIT, UInputNative.BTN_RIGHT);
                EnableBit(UInputNative.UI_SET_KEYBIT, UInputNative.BTN_MIDDLE);

                if (_width > 0 && _height > 0)
                {
                    // ABSOLUTE MODE
                    EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_ABS);
                    EnableBit(UInputNative.UI_SET_ABSBIT, UInputNative.ABS_X);
                    EnableBit(UInputNative.UI_SET_ABSBIT, UInputNative.ABS_Y);
                    EnableBit(UInputNative.UI_SET_PROPBIT, UInputNative.INPUT_PROP_DIRECT);
                    
                    // REL mode (Scroll + Relative Movements even in Absolute mode)
                    EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_REL);
                    EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_WHEEL);
                    EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_X);
                    EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_Y);
                    
                    Log.Information("[UInputDevice] Creating ABSOLUTE mode device (EV_ABS + INPUT_PROP_DIRECT)");
                }
                else
                {
                    // RELATIVE MODE
                    EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_REL);
                    EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_X);
                    EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_Y);
                    EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_WHEEL);
                    Log.Information("[UInputDevice] Creating RELATIVE mode device");
                }

                for (int keyCode = 1; keyCode <= 255; keyCode++)
                {
                    EnableBit(UInputNative.UI_SET_KEYBIT, keyCode);
                }



                var uidev = new UInputNative.uinput_user_dev
                {
                    name = "CrossMacro Virtual Input Device",
                    id_bustype = UInputNative.BUS_USB,
                    id_vendor = 0x1234,
                    id_product = 0x5678,
                    id_version = 1,
                    absmax = new int[64],
                    absmin = new int[64],
                    absfuzz = new int[64],
                    absflat = new int[64]
                };

                if (_width > 0 && _height > 0)
                {
                    uidev.absmax[UInputNative.ABS_X] = _width;
                    uidev.absmax[UInputNative.ABS_Y] = _height;
                }

                // Write device setup
                IntPtr size = (IntPtr)Marshal.SizeOf<UInputNative.uinput_user_dev>();
                
                // Use the specific write_setup method for uinput_user_dev
                IntPtr result = UInputNative.write_setup(_fd, ref uidev, size);
                if (result.ToInt32() < 0)
                {
                    var errno = Marshal.GetLastWin32Error();
                    Log.Error("[UInputDevice] Failed to write uinput_user_dev. Errno: {Errno}", errno);
                    throw new InvalidOperationException($"Failed to write uinput_user_dev (Errno: {errno})");
                }

                int createResult = UInputNative.ioctl(_fd, UInputNative.UI_DEV_CREATE, 0);
                if (createResult < 0)
                {
                    var errno = Marshal.GetLastWin32Error();
                    Log.Error("[UInputDevice] Failed to create device (UI_DEV_CREATE). Errno: {Errno}", errno);
                    throw new InvalidOperationException($"Failed to create device (Errno: {errno})");
                }
                
                Log.Information("[UInputDevice] Virtual input device (mouse + keyboard) created successfully.");
                
                // Wait for kernel to fully register the device before sending events
                // This prevents missed clicks during fast playback start
                Thread.Sleep(100);
                Log.Debug("[UInputDevice] Kernel stabilization delay complete");
            }
            catch
            {
                UInputNative.close(_fd);
                _fd = -1;
                throw;
            }
        }

        private void EnableBit(uint request, int bit)
        {
            if (UInputNative.ioctl(_fd, request, bit) < 0)
            {
                var errno = Marshal.GetLastWin32Error();
                Log.Error("[UInputDevice] Failed to enable bit {Bit} for request {Request}. Errno: {Errno}", bit, request, errno);
                throw new InvalidOperationException($"Failed to enable bit {bit} (Errno: {errno})");
            }
        }

        public void SendEvent(ushort type, ushort code, int value)
        {
            if (_fd < 0) return;

            var ev = new UInputNative.input_event
            {
                type = type,
                code = code,
                value = value,
                time_sec = IntPtr.Zero,
                time_usec = IntPtr.Zero
            };

            IntPtr size = (IntPtr)Marshal.SizeOf<UInputNative.input_event>();
            IntPtr result = UInputNative.write(_fd, ref ev, size);
            
            if (result.ToInt32() < 0)
            {
                var errno = Marshal.GetLastWin32Error();
                Log.Warning("[UInputDevice] Failed to write event. Errno: {Errno}", errno);
            }
            else
            {
                // Uncomment for verbose logging
                // Log.Verbose("[UInputDevice] Sent Event: Type={Type}, Code={Code}, Value={Value}", type, code, value);
            }
        }

        private void Emit(ushort type, ushort code, int value)
        {
            SendEvent(type, code, value);
        }

        public void Move(int dx, int dy)
        {
            if (_fd < 0) return;

            Emit(UInputNative.EV_REL, UInputNative.REL_X, dx);
            Emit(UInputNative.EV_REL, UInputNative.REL_Y, dy);
            Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }

        public void MoveAbsolute(int x, int y)
        {
            if (_fd < 0) return;

            // Clamp to screen bounds if resolution is known
            if (_width > 0) x = Math.Clamp(x, 0, _width);
            if (_height > 0) y = Math.Clamp(y, 0, _height);

            Emit(UInputNative.EV_ABS, UInputNative.ABS_X, x);
            Emit(UInputNative.EV_ABS, UInputNative.ABS_Y, y);
            Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }

        public void Click(int buttonCode, bool pressed)
        {
            Emit(UInputNative.EV_KEY, (ushort)buttonCode, pressed ? 1 : 0);
            Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }

        public void EmitButton(int buttonCode, bool pressed)
        {
            SendEvent(UInputNative.EV_KEY, (ushort)buttonCode, pressed ? 1 : 0);
            SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }

        public void EmitClick(int buttonCode)
        {
            EmitButton(buttonCode, true);
            EmitButton(buttonCode, false);
        }
        
        /// <summary>
        /// Emit a keyboard key press or release
        /// </summary>
        /// <param name="keyCode">Linux input key code (e.g., 30 for KEY_A)</param>
        /// <param name="pressed">True for key press, false for release</param>
        public void EmitKey(int keyCode, bool pressed)
        {
            SendEvent(UInputNative.EV_KEY, (ushort)keyCode, pressed ? 1 : 0);
            SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_fd >= 0)
                {
                    Log.Information("[UInputDevice] Destroying virtual device...");
                    UInputNative.ioctl(_fd, UInputNative.UI_DEV_DESTROY, 0);
                    UInputNative.close(_fd);
                    _fd = -1;
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
