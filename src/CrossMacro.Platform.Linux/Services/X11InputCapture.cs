using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Services
{
    /// <summary>
    /// Facade/Composite for X11 Input Capture.
    /// Manages both Absolute (Clamped) and Relative (Raw) capture strategies.
    /// Acts as a single entry point for dependency injection but delegates work to child captures.
    /// </summary>
    public class X11InputCapture : IInputCapture
    {
        private readonly X11AbsoluteCapture _absoluteCapture;
        private readonly X11RelativeCapture _relativeCapture;
        private readonly ISettingsService _settingsService;
        
        // Track active capturers
        private bool _disposed;
        
        public string ProviderName => "X11 Facade (Abs/Raw)";

        public bool IsSupported => _absoluteCapture.IsSupported || _relativeCapture.IsSupported;

        public event EventHandler<InputCaptureEventArgs>? InputReceived;
        public event EventHandler<string>? Error;

        public X11InputCapture(
            X11AbsoluteCapture absoluteCapture, 
            X11RelativeCapture relativeCapture,
            ISettingsService settingsService)
        {
            _absoluteCapture = absoluteCapture;
            _relativeCapture = relativeCapture;
            _settingsService = settingsService;
            
            _absoluteCapture.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
            _absoluteCapture.Error += (s, e) => Error?.Invoke(this, e);
            
            _relativeCapture.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
            _relativeCapture.Error += (s, e) => Error?.Invoke(this, e);
        }

        public void Configure(bool captureMouse, bool captureKeyboard)
        {
            _absoluteCapture.Configure(captureMouse, captureKeyboard);
            _relativeCapture.Configure(captureMouse, captureKeyboard);
        }



        public async Task StartAsync(CancellationToken ct)
        {
            bool useRelative = _settingsService.Current.ForceRelativeCoordinates;

            if (useRelative)
            {
                // Force Relative (Raw) Mode
                // Only start Relative Capture
                await _relativeCapture.StartAsync(ct);
            }
            else
            {
                // Absolute (Standard) Mode
                // Only start Absolute Capture
                await _absoluteCapture.StartAsync(ct);
            }
        }

        public void Stop()
        {
            _absoluteCapture.Stop();
            _relativeCapture.Stop();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _absoluteCapture.Dispose();
            _relativeCapture.Dispose();
            _disposed = true;
        }
    }
}
