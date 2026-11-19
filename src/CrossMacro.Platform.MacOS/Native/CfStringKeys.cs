using System.Collections.Concurrent;

namespace CrossMacro.Platform.MacOS.Native;

/// <summary>
/// CFStrings for the fixed set of Accessibility attribute, action and window-dictionary key names.
/// Each is created once and kept for the lifetime of the process, so lookups do not allocate native strings.
/// Only compile-time constant names may be passed, which keeps the cache bounded.
/// </summary>
internal static class CfStringKeys
{
    private static readonly ConcurrentDictionary<string, Lazy<IntPtr>> Keys = new(StringComparer.Ordinal);

    internal static IntPtr Get(string name) => Keys.GetOrAdd(name, static key => new Lazy<IntPtr>(() => Create(key))).Value;

    private static IntPtr Create(string name)
    {
        var value = CoreFoundation.CFStringCreateWithCString(IntPtr.Zero, name, CoreFoundation.kCFStringEncodingUtf8);
        return value != IntPtr.Zero
            ? value
            : throw new InvalidOperationException("Failed to allocate a Core Foundation string.");
    }
}
