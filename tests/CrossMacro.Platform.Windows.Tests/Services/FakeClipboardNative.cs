using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Tests.Services;

internal sealed class FakeClipboardNative : IWindowsClipboardNative, IDisposable
{
    private readonly Dictionary<IntPtr, int> _sizes = [];
    private readonly Dictionary<IntPtr, int> _allocationNumbers = [];
    private readonly Dictionary<uint, IntPtr> _readable = [];
    private int _allocationCalls;

    public bool CanOpen { get; init; } = true;
    public bool CanEmpty { get; init; } = true;
    public int FailedAllocation { get; init; }
    public int FailedLock { get; init; }
    public uint RejectedFormat { get; init; }
    public uint ThrowOnFormat { get; init; }
    public int CloseCalls { get; private set; }
    public int UnlockCalls { get; private set; }
    public List<IntPtr> Allocated { get; } = [];
    public List<IntPtr> Freed { get; } = [];
    public List<IntPtr> Transferred { get; } = [];
    public Dictionary<uint, byte[]> Published { get; } = [];

    /// <summary>Puts data on the fake clipboard so that the read side can find it.</summary>
    public void SetReadable(uint format, byte[] bytes)
    {
        var memory = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
        Marshal.Copy(bytes, 0, memory, bytes.Length);
        _sizes.Add(memory, bytes.Length);
        _readable[format] = memory;
    }

    public IntPtr Allocate(nuint byteCount)
    {
        _allocationCalls++;
        if (_allocationCalls == FailedAllocation)
        {
            return IntPtr.Zero;
        }

        var memory = Marshal.AllocHGlobal(checked((int)byteCount));
        _sizes.Add(memory, checked((int)byteCount));
        _allocationNumbers.Add(memory, _allocationCalls);
        Allocated.Add(memory);
        return memory;
    }

    public IntPtr Lock(IntPtr handle) =>
        _allocationNumbers.TryGetValue(handle, out var allocationNumber) && allocationNumber == FailedLock
            ? IntPtr.Zero
            : handle;

    public bool Unlock(IntPtr handle)
    {
        UnlockCalls++;
        return true;
    }

    public IntPtr Free(IntPtr handle)
    {
        Freed.Add(handle);
        if (_sizes.Remove(handle))
        {
            _ = _allocationNumbers.Remove(handle);
            Marshal.FreeHGlobal(handle);
        }
        return IntPtr.Zero;
    }

    public bool Open(IntPtr owner) => CanOpen;
    public bool Empty() => CanEmpty;

    public bool Close()
    {
        CloseCalls++;
        return true;
    }

    public IntPtr SetData(uint format, IntPtr handle)
    {
        if (format == ThrowOnFormat)
        {
            throw new InvalidOperationException("publish failed");
        }
        if (format == RejectedFormat)
        {
            return IntPtr.Zero;
        }

        var bytes = new byte[_sizes[handle]];
        Marshal.Copy(handle, bytes, 0, bytes.Length);
        Published.Add(format, bytes);
        Transferred.Add(handle);
        return handle;
    }

    public bool IsFormatAvailable(uint format) => _readable.ContainsKey(format);

    public IntPtr GetData(uint format) => _readable.GetValueOrDefault(format);

    public nuint Size(IntPtr handle) => (nuint)_sizes[handle];

    public void Dispose()
    {
        // The fake system also releases successfully transferred buffers at test teardown.
        foreach (var handle in _sizes.Keys)
        {
            Marshal.FreeHGlobal(handle);
        }
        _sizes.Clear();
        _allocationNumbers.Clear();
        _readable.Clear();
    }
}
