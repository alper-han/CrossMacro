using System;

namespace CrossMacro.Platform.Abstractions;

public sealed class InputInjectionPermissionRequiredException : InvalidOperationException
{
    public InputInjectionPermissionRequiredException(string message)
        : base(message)
    {
    }
}
