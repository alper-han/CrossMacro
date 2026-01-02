using System;
using System.Runtime.InteropServices;
using System.Threading;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Native.X11;

namespace CrossMacro.Platform.Linux.Services
{
    /// <summary>
    /// Captures input using XInput2 but relies on XQueryPointer for absolute position data.
    /// Used for "Absolute Mouse" mode where reliable screen mapping is prioritized over raw motion.
    /// </summary>
    public class X11AbsoluteCapture : X11CaptureBase
    {
        private double _lastX = -1;
        private double _lastY = -1;
        
        // State for motion compression
        private bool _pendingMotion = false;
        private long _lastMotionTime = 0;
        private const int MinMotionIntervalMs = 10; // ~100Hz cap

        public override string ProviderName => "X11 (Absolute Motion)";

        protected override void OnCaptureStarted()
        {
            // Initial position sync
            if (X11Native.XQueryPointer(_display, _rootWindow, out _, out _, out int rootX, out int rootY, out _, out _, out _))
            {
                _lastX = rootX;
                _lastY = rootY;
            }
        }

        protected override void OnLoopIdle()
        {
            if (_pendingMotion)
            {
                ProcessPendingMotion();
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        protected override void FlushPendingMotion()
        {
            if (_pendingMotion) ProcessPendingMotion();
        }

        protected override void ProcessMotion(XGenericEventCookie cookie)
        {

            
            _pendingMotion = true;
                
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if ((now - _lastMotionTime) >= MinMotionIntervalMs)
            {
                ProcessPendingMotion();
            }
        }

        private void ProcessPendingMotion()
        {
            _pendingMotion = false;
            _lastMotionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (!X11Native.XQueryPointer(_display, _rootWindow, out _, out _, out int rootX, out int rootY, out _, out _, out _))
            {
                return;
            }

            if (_lastX < 0)
            {
                _lastX = rootX;
                _lastY = rootY;
                return;
            }

            double dx = rootX - _lastX;
            double dy = rootY - _lastY;
            
            _lastX = rootX;
            _lastY = rootY;
            

            if (dx == 0 && dy == 0) return;

            int moveX = (int)dx;
            int moveY = (int)dy;

            if (moveX != 0)
            {
                var argsX = new InputCaptureEventArgs
                {
                    Type = InputEventType.MouseMove,
                    Code = 0,
                    Value = moveX,
                    Timestamp = _lastMotionTime,
                    DeviceName = ProviderName
                };
                OnInputReceived(argsX);
            }

            if (moveY != 0)
            {
                var argsY = new InputCaptureEventArgs
                {
                    Type = InputEventType.MouseMove,
                    Code = 1,
                    Value = moveY,
                    Timestamp = _lastMotionTime,
                    DeviceName = ProviderName
                };
                OnInputReceived(argsY);
            }
            
            // SYNC
            var argsSync = new InputCaptureEventArgs
            {
                Type = InputEventType.Sync,
                Timestamp = _lastMotionTime,
                DeviceName = ProviderName
            };
            OnInputReceived(argsSync);
        }
    }
}
