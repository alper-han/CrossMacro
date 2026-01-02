using System;
using System.Runtime.InteropServices;
using System.Threading;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Native.X11;
using Serilog;

namespace CrossMacro.Platform.Linux.Services
{
    /// <summary>
    /// Captures raw XInput2 events for true relative motion (unclamped by screen bounds).
    /// Used for FPS games and infinite spin scenarios.
    /// </summary>
    public class X11RelativeCapture : X11CaptureBase
    {
        private double _accumulatorX;
        private double _accumulatorY;
        
        // Configurable setting (could be injected if needed, hardcoded for now per request)
        private const bool SkipZeroDeltas = true;

        public override string ProviderName => "X11 (Raw Relative)";

        protected override void OnCaptureStarted()
        {
            Log.Information("[X11RelCapture] Started capturing (Raw Mode)");
        }

        protected override void ProcessMotion(XGenericEventCookie cookie)
        {
             var rawEvent = Marshal.PtrToStructure<XIRawEvent>(cookie.data);
             ProcessRawMotion(rawEvent);
        }

        private void ProcessRawMotion(XIRawEvent rawEvent)
        {
            double dx = 0;
            double dy = 0;
            
            int valueIndex = 0;
            var maskState = rawEvent.valuators;
            
            if (XInput2Consts.IsBitSet(maskState.mask, maskState.mask_len, 0))
            {
                dx = ReadDouble(rawEvent.raw_values, valueIndex);
                valueIndex++;
            }
            
            if (XInput2Consts.IsBitSet(maskState.mask, maskState.mask_len, 1))
            {
               dy = ReadDouble(rawEvent.raw_values, valueIndex);
               valueIndex++;
            }
            
            if (SkipZeroDeltas && Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            {
                return;
            }

            _accumulatorX += dx;
            _accumulatorY += dy;
            
            int moveX = (int)_accumulatorX;
            int moveY = (int)_accumulatorY;
            
            if (moveX == 0 && moveY == 0) return;
            
            if (moveX != 0)
            {
                var argsX = new InputCaptureEventArgs
                {
                    Type = InputEventType.MouseMove,
                    Code = 0,
                    Value = moveX,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DeviceName = ProviderName
                };
                OnInputReceived(argsX);
                _accumulatorX -= moveX;
            }
            
            if (moveY != 0)
            {
                var argsY = new InputCaptureEventArgs
                {
                    Type = InputEventType.MouseMove,
                    Code = 1,
                    Value = moveY,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DeviceName = ProviderName
                };
                OnInputReceived(argsY);
                _accumulatorY -= moveY;
            }
            
            OnInputReceived(new InputCaptureEventArgs
            {
                Type = InputEventType.Sync,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DeviceName = ProviderName
            });
        }
        
        private double ReadDouble(IntPtr ptr, int index)
        {
            IntPtr targetPtr = IntPtr.Add(ptr, index * 8);
            long val = Marshal.ReadInt64(targetPtr);
            return BitConverter.Int64BitsToDouble(val);
        }
    }
}
