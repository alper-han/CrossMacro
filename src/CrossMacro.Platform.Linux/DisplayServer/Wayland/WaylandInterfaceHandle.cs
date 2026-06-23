using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandInterfaceHandle : IDisposable
{
    private readonly WlCString _name;
    private readonly WlMessage[] _methods;
    private readonly WlMessage[] _events;
    private readonly List<WlCString> _strings = [];
    private readonly List<GCHandle> _typeHandles = [];
    private readonly GCHandle _methodsHandle;
    private readonly GCHandle _eventsHandle;
    private readonly GCHandle _interfaceHandle;

    public WaylandInterfaceHandle(string name, int version, (string Name, string Signature)[] methods, (string Name, string Signature)[] events)
    {
        _name = new WlCString(name);
        _methods = BuildMessages(methods);
        _events = BuildMessages(events);
        _methodsHandle = GCHandle.Alloc(_methods, GCHandleType.Pinned);
        _eventsHandle = GCHandle.Alloc(_events, GCHandleType.Pinned);
        _interfaceHandle = GCHandle.Alloc(new WlInterface
        {
            Name = _name.Address,
            Version = version,
            MethodCount = _methods.Length,
            Methods = _methodsHandle.AddrOfPinnedObject(),
            EventCount = _events.Length,
            Events = _eventsHandle.AddrOfPinnedObject()
        }, GCHandleType.Pinned);
    }

    public IntPtr Address => _interfaceHandle.AddrOfPinnedObject();

    public void SetMethodTypes(int methodIndex, params IntPtr[] typePointers)
    {
        var typed = new IntPtr[typePointers.Length];
        Array.Copy(typePointers, typed, typePointers.Length);
        var handle = GCHandle.Alloc(typed, GCHandleType.Pinned);
        _typeHandles.Add(handle);
        _methods[methodIndex].Types = handle.AddrOfPinnedObject();
    }

    public void Dispose()
    {
        foreach (var handle in _typeHandles)
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        _interfaceHandle.Free();
        _methodsHandle.Free();
        _eventsHandle.Free();
        _name.Dispose();
        foreach (var item in _strings)
        {
            item.Dispose();
        }
    }

    private WlMessage[] BuildMessages((string Name, string Signature)[] definitions)
    {
        var result = new WlMessage[definitions.Length];
        for (var i = 0; i < definitions.Length; i++)
        {
            var name = new WlCString(definitions[i].Name);
            var signature = new WlCString(definitions[i].Signature);
            _strings.Add(name);
            _strings.Add(signature);
            result[i] = new WlMessage { Name = name.Address, Signature = signature.Address, Types = IntPtr.Zero };
        }

        return result;
    }
}
