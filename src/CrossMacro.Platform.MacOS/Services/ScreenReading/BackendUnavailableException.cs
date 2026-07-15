using System;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class BackendUnavailableException(string message) : InvalidOperationException(message);
