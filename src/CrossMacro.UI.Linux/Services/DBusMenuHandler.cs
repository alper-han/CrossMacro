using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Tmds.DBus.Protocol;
using static CrossMacro.UI.Linux.Services.LinuxStatusNotifierTrayHandle;

namespace CrossMacro.UI.Linux.Services;

/// <summary>The com.canonical.dbusmenu interface: serves the tray menu layout and routes menu events to the Avalonia items.</summary>
internal sealed class DBusMenuHandler : DBusInterfaceHandler, IDisposable
{
    private static readonly string[] Properties = ["Version", "TextDirection", "Status", "IconThemePath"];
    private static readonly byte[] Xml =
        "<interface name=\"com.canonical.dbusmenu\"><property name=\"Version\" type=\"u\" access=\"read\"/><property name=\"TextDirection\" type=\"s\" access=\"read\"/><property name=\"Status\" type=\"s\" access=\"read\"/><property name=\"IconThemePath\" type=\"as\" access=\"read\"/><method name=\"GetLayout\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"as\"/><arg direction=\"out\" type=\"u\"/><arg direction=\"out\" type=\"(ia{sv}av)\"/></method><method name=\"GetGroupProperties\"><arg direction=\"in\" type=\"ai\"/><arg direction=\"in\" type=\"as\"/><arg direction=\"out\" type=\"a(ia{sv})\"/></method><method name=\"GetProperty\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"s\"/><arg direction=\"out\" type=\"v\"/></method><method name=\"Event\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"s\"/><arg direction=\"in\" type=\"v\"/><arg direction=\"in\" type=\"u\"/></method><method name=\"EventGroup\"><arg direction=\"in\" type=\"a(isvu)\"/><arg direction=\"out\" type=\"ai\"/></method><method name=\"AboutToShow\"><arg direction=\"in\" type=\"i\"/><arg direction=\"out\" type=\"b\"/></method><method name=\"AboutToShowGroup\"><arg direction=\"in\" type=\"ai\"/><arg direction=\"out\" type=\"ai\"/><arg direction=\"out\" type=\"ai\"/></method><signal name=\"LayoutUpdated\"><arg type=\"u\"/><arg type=\"i\"/></signal></interface>"u8.ToArray();
    private readonly LinuxStatusNotifierTrayHandle _owner;
    private readonly DBusMenuModel _model;
    private uint _revision = 1;
    private bool _disposed;

    public DBusMenuHandler(LinuxStatusNotifierTrayHandle owner, NativeMenu menu) : base(MenuObjectPath, MenuInterface)
    {
        _owner = owner;
        _model = new DBusMenuModel(menu);
        _model.Changed += OnModelChanged;
    }

    protected override ReadOnlyMemory<byte> InterfaceXml => Xml;
    protected override string[] PropertyNames => Properties;

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _model.Changed -= OnModelChanged;
        _model.Dispose();
    }

    protected override void WriteProperty(ref MessageWriter writer, string property)
    {
        switch (property)
        {
            case "Version": writer.WriteVariantUInt32(4); break;
            case "TextDirection": writer.WriteVariantString("ltr"); break;
            case "Status": writer.WriteVariantString("normal"); break;
            case "IconThemePath": writer.WriteSignature("as"); writer.WriteArray(Array.Empty<string>()); break;
            default: throw new InvalidOperationException("Unknown menu property.");
        }
    }

    protected override async ValueTask DispatchAsync(MethodContext context)
    {
        // NativeMenu is Avalonia state: all traversal and event dispatch must run on its UI thread.
        await Dispatcher.UIThread.InvokeAsync(() => DispatchOnUIThread(context)).GetTask().ConfigureAwait(false);
    }

    private static void ReplyInvalidId(MethodContext context) => context.ReplyError("com.canonical.dbusmenu.Error.InvalidMenuItem", "Unknown menu item.");

    private void OnModelChanged(object? sender, EventArgs args)
    {
        unchecked { _revision++; }
        _owner.EmitLayoutUpdated(_revision);
    }

    private void DispatchOnUIThread(MethodContext context)
    {
        if (_disposed || _owner.IsDisposed)
        {
            context.ReplyError("org.freedesktop.DBus.Error.Disconnected", "Tray is disposed.");
            return;
        }
        var member = context.Request.MemberAsString;
        var signature = member switch
        {
            "GetLayout" => "iias",
            "GetGroupProperties" => "aias",
            "GetProperty" => "is",
            "Event" => "isvu",
            "EventGroup" => "a(isvu)",
            "AboutToShow" => "i",
            "AboutToShowGroup" => "ai",
            _ => null,
        };
        if (signature is null) { context.ReplyUnknownMethodError(); return; }
        if (!CheckSignature(context, signature)) { return; }
        var reader = context.Request.GetBodyReader();
        switch (member)
        {
            case "GetLayout": HandleGetLayout(context, ref reader); break;
            case "GetGroupProperties": HandleGetGroupProperties(context, ref reader); break;
            case "GetProperty": HandleGetProperty(context, ref reader); break;
            case "Event": HandleEvent(context, ref reader); break;
            case "EventGroup": HandleEventGroup(context, ref reader); break;
            case "AboutToShow": HandleAboutToShow(context, ref reader); break;
            case "AboutToShowGroup": HandleAboutToShowGroup(context, ref reader); break;
        }
    }

    private void HandleGetLayout(MethodContext context, ref Reader reader)
    {
        var parent = reader.ReadInt32();
        var depth = reader.ReadInt32();
        var names = reader.ReadArrayOfString();
        if (!_model.Contains(parent)) { ReplyInvalidId(context); return; }
        if (depth < -1) { context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Invalid recursion depth."); return; }
        var layoutWriter = context.CreateReplyWriter("u(ia{sv}av)");
        try
        {
            layoutWriter.WriteUInt32(_revision);
            _model.WriteLayout(ref layoutWriter, parent, depth, names);
            context.Reply(layoutWriter.CreateMessage());
        }
        finally { layoutWriter.Dispose(); }
    }

    private void HandleGetGroupProperties(MethodContext context, ref Reader reader)
    {
        var ids = reader.ReadArrayOfInt32();
        var properties = reader.ReadArrayOfString();
        var groupWriter = context.CreateReplyWriter("a(ia{sv})");
        try
        {
            var array = groupWriter.WriteArrayStart(DBusType.Struct);
            if (ids.Length is 0)
            {
                _model.WriteGroupItem(ref groupWriter, 0, properties);
                foreach (var id in _model.ItemIds) { _model.WriteGroupItem(ref groupWriter, id, properties); }
            }
            else { foreach (var id in ids) { if (_model.Contains(id)) { _model.WriteGroupItem(ref groupWriter, id, properties); } } }
            groupWriter.WriteArrayEnd(array);
            context.Reply(groupWriter.CreateMessage());
        }
        finally { groupWriter.Dispose(); }
    }

    private void HandleGetProperty(MethodContext context, ref Reader reader)
    {
        var itemId = reader.ReadInt32();
        var name = reader.ReadString();
        if (!_model.Contains(itemId)) { ReplyInvalidId(context); return; }
        if (!_model.TryGetItemProperty(itemId, name, out var value))
        {
            context.ReplyError("com.canonical.dbusmenu.Error.InvalidProperty", "Unknown menu item property.");
            return;
        }
        using var writer = context.CreateReplyWriter("v");
        writer.WriteVariant(value);
        context.Reply(writer.CreateMessage());
    }

    private void HandleEvent(MethodContext context, ref Reader reader)
    {
        var eventItem = reader.ReadInt32();
        var eventName = reader.ReadString();
        _ = reader.ReadVariantValue();
        _ = reader.ReadUInt32();
        if (!_model.Contains(eventItem)) { ReplyInvalidId(context); return; }
        _model.TriggerEvent(eventItem, eventName);
        ReplyEmpty(context);
    }

    private void HandleEventGroup(MethodContext context, ref Reader reader)
    {
        var errors = new List<int>();
        var events = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(events))
        {
            reader.AlignStruct();
            var id = reader.ReadInt32();
            var eventId = reader.ReadString();
            _ = reader.ReadVariantValue();
            _ = reader.ReadUInt32();
            if (_model.Contains(id)) { _model.TriggerEvent(id, eventId); } else { errors.Add(id); }
        }
        using var writer = context.CreateReplyWriter("ai");
        writer.WriteArray(errors);
        context.Reply(writer.CreateMessage());
    }

    private void HandleAboutToShow(MethodContext context, ref Reader reader)
    {
        if (!_model.Contains(reader.ReadInt32())) { ReplyInvalidId(context); return; }
        using var writer = context.CreateReplyWriter("b");
        writer.WriteBool(value: false);
        context.Reply(writer.CreateMessage());
    }

    private void HandleAboutToShowGroup(MethodContext context, ref Reader reader)
    {
        var requested = reader.ReadArrayOfInt32();
        var invalid = requested.Where(id => !_model.Contains(id)).ToList();
        using var writer = context.CreateReplyWriter("aiai");
        writer.WriteArray(Array.Empty<int>());
        writer.WriteArray(invalid);
        context.Reply(writer.CreateMessage());
    }
}
