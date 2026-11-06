using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CrossMacro.UI.Services.Desktop;
using Tmds.DBus.Protocol;

namespace CrossMacro.UI.Linux.Services;

internal sealed class LinuxStatusNotifierTrayHandle : ITrayIconHandle
{
    private const string SniObjectPath = "/StatusNotifierItem";
    private const string MenuObjectPath = "/StatusNotifierMenu";
    private const string SniInterface = "org.kde.StatusNotifierItem";
    private const string MenuInterface = "com.canonical.dbusmenu";
    private const string WatcherBusName = "org.kde.StatusNotifierWatcher";
    private const string WatcherObjectPath = "/StatusNotifierWatcher";
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly MenuHandler _menuHandler;
    private readonly ItemHandler _itemHandler;
    private readonly int _iconWidth;
    private readonly int _iconHeight;
    private readonly byte[] _iconPixmap;
    private DBusConnection? _connection;
    private string _toolTipText;
#pragma warning disable IDE0032 // Field backing property has custom signal dispatch.
    private bool _isVisible = true;
#pragma warning restore IDE0032
    private bool _isDisposed;
    private bool _isRegistered;
    public event EventHandler? Clicked;

    public LinuxStatusNotifierTrayHandle(string? sessionBusAddress, NativeMenu menu, string toolTipText,
        (int Width, int Height, byte[] Pixmap) icon)
    {
        ArgumentNullException.ThrowIfNull(menu);
        Dispatcher.UIThread.VerifyAccess();
        _toolTipText = toolTipText;
        (_iconWidth, _iconHeight, _iconPixmap) = icon;
        _itemHandler = new ItemHandler(this);
        _menuHandler = new MenuHandler(this, menu);
        if (string.IsNullOrWhiteSpace(sessionBusAddress))
        {
            _isRegistered = true;
        }
        else
        {
            _ = RunBusAsync(sessionBusAddress, _shutdown.Token);
        }
    }

    public bool IsVisible
    {
        get { lock (_lock) { return _isVisible; } }
        set
        {
            lock (_lock)
            {
                if (_isDisposed || _isVisible == value) { return; }
                _isVisible = value;
                EmitSignal(SniObjectPath, SniInterface, "NewStatus", value ? "Active" : "Passive");
            }
        }
    }

    public bool HasNativeMenuExporter { get { lock (_lock) { return _isRegistered && !_isDisposed; } } }
    public string? GetToolTipText() { lock (_lock) { return _toolTipText; } }

    public void SetToolTipText(string? value)
    {
        lock (_lock)
        {
            if (_isDisposed || string.Equals(_toolTipText, value ?? string.Empty, StringComparison.Ordinal)) { return; }
            _toolTipText = value ?? string.Empty;
            EmitSignal(SniObjectPath, SniInterface, "NewTitle");
            EmitSignal(SniObjectPath, SniInterface, "NewToolTip");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) { return; }
            _isDisposed = true;
            _isRegistered = false;
            _shutdown.Cancel();
            _connection?.Dispose();
            _connection = null;
            _shutdown.Dispose();
        }
        if (Dispatcher.UIThread.CheckAccess()) { _menuHandler.Dispose(); }
        else { Dispatcher.UIThread.Post(_menuHandler.Dispose); }
    }

    private async Task RunBusAsync(string address, CancellationToken cancellationToken)
    {
        using var connection = new DBusConnection(address);
        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);
            lock (_lock)
            {
                if (_isDisposed) { return; }
                connection.AddMethodHandler(_itemHandler);
                connection.AddMethodHandler(_menuHandler);
                _connection = connection;
            }
            using var watcher = await connection.WatchNameOwnerAsync(WatcherBusName).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                var owner = await watcher.WaitForOwnerAsync(cancellationToken).ConfigureAwait(false);
                using var ownerChanged = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, watcher.GetOwnerChangedCancellationToken(owner));
                try
                {
                    await RegisterItemAsync(connection, owner).WaitAsync(ownerChanged.Token).ConfigureAwait(false);
                    lock (_lock) { _isRegistered = true; }
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, ownerChanged.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ownerChanged.IsCancellationRequested)
                {
                    // Register again when a different watcher owns the name.
                }
                catch (DBusErrorReplyException ex)
                {
                    Serilog.Log.Warning(ex, "[LinuxTray] StatusNotifierItem registration was rejected.");
                    // Do not spin on a rejecting host; wait for its owner to change.
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, ownerChanged.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (ownerChanged.IsCancellationRequested) { /* Ignored */ }
                }
                finally
                {
                    lock (_lock) { _isRegistered = false; }
                }
            }
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is not OutOfMemoryException)
        {
            // Disposing the connection interrupts pending connection and bus operations.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Warning(ex, "[LinuxTray] Session bus connection or watcher monitor failed.");
        }
        finally
        {
            lock (_lock)
            {
                _isRegistered = false;
                if (ReferenceEquals(_connection, connection)) { _connection = null; }
            }
        }
    }

    private static async Task RegisterItemAsync(DBusConnection connection, string owner)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(destination: owner, path: WatcherObjectPath,
                @interface: WatcherBusName, member: "RegisterStatusNotifierItem", signature: "s");
            writer.WriteString(SniObjectPath);
            message = writer.CreateMessage();
        }
        await connection.CallMethodAsync(message).ConfigureAwait(false);
        Serilog.Log.Information("[LinuxTray] StatusNotifierItem registered with host watcher.");
    }

    private void EmitSignal(string path, string iface, string member, string? value = null)
    {
        lock (_lock)
        {
            if (_connection is not { } connection || _isDisposed) { return; }
            try
            {
                using var writer = connection.GetMessageWriter();
                writer.WriteSignalHeader(path: path, @interface: iface, member: member,
                    signature: value is null ? null : "s");
                if (value is not null) { writer.WriteString(value); }
                if (!connection.TrySendMessage(writer.CreateMessage()))
                {
                    Serilog.Log.Debug("[LinuxTray] Cannot emit {Member}: session bus is not connected.", member);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Serilog.Log.Debug(ex, "[LinuxTray] Failed to emit {Member}", member);
            }
        }
    }
    private void Activate()
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_lock)
            {
                if (_isDisposed) { return; }
            }
            Clicked?.Invoke(this, EventArgs.Empty);
        });
    }


    private void WritePixmaps(ref MessageWriter writer)
    {
        var array = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteStructureStart();
        writer.WriteInt32(_iconWidth);
        writer.WriteInt32(_iconHeight);
        writer.WriteArray(_iconPixmap);
        writer.WriteArrayEnd(array);
    }

    private static bool CheckSignature(MethodContext context, string signature)
    {
        if (string.Equals(context.Request.SignatureAsString, signature, StringComparison.Ordinal)) { return true; }
        context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Unexpected method signature.");
        return false;
    }

    private static void ReplyEmpty(MethodContext context)
    {
        using var writer = context.CreateReplyWriter(signature: null);
        context.Reply(writer.CreateMessage());
    }

    private abstract class Handler(string path, string interfaceName) : IPathMethodHandler
    {
        public string Path => path;
        public bool HandlesChildPaths => false;
        protected abstract ReadOnlyMemory<byte> InterfaceXml { get; }
        protected abstract string[] PropertyNames { get; }
        protected abstract void WriteProperty(ref MessageWriter writer, string property);
        protected abstract ValueTask DispatchAsync(MethodContext context);

        public async ValueTask HandleMethodAsync(MethodContext context)
        {
            try
            {
                if (context.IsDBusIntrospectRequest)
                {
                    context.ReplyIntrospectXml([InterfaceXml]);
                    return;
                }
                if (context.Request.InterfaceAsString is "org.freedesktop.DBus.Properties")
                {
                    HandleProperties(context);
                    return;
                }
                if (!string.Equals(context.Request.InterfaceAsString, interfaceName, StringComparison.Ordinal))
                {
                    context.ReplyUnknownMethodError();
                    return;
                }
                await DispatchAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Serilog.Log.Error(ex, "[LinuxTray] Request {Interface}.{Member} failed.",
                    context.Request.InterfaceAsString, context.Request.MemberAsString);
                context.ReplyError("org.freedesktop.DBus.Error.Failed", "Tray request failed.");
            }
        }

        private void HandleProperties(MethodContext context)
        {
            var member = context.Request.MemberAsString;
            if (member is not ("Get" or "GetAll" or "Set")) { context.ReplyUnknownMethodError(); return; }
            if (!CheckSignature(context, member switch { "Get" => "ss", "Set" => "ssv", _ => "s" })) { return; }
            var reader = context.Request.GetBodyReader();
            if (!string.Equals(reader.ReadString(), interfaceName, StringComparison.Ordinal))
            {
                context.ReplyError("org.freedesktop.DBus.Error.UnknownInterface", "Unknown interface.");
                return;
            }
            if (member is "GetAll")
            {
                var writer = context.CreateReplyWriter("a{sv}");
                try
                {
                    var array = writer.WriteDictionaryStart();
                    foreach (var property in PropertyNames)
                    {
                        writer.WriteDictionaryEntryStart();
                        writer.WriteString(property);
                        WriteProperty(ref writer, property);
                    }
                    writer.WriteDictionaryEnd(array);
                    context.Reply(writer.CreateMessage());
                }
                finally { writer.Dispose(); }
                return;
            }
            var name = reader.ReadString();
            if (Array.IndexOf(PropertyNames, name) < 0)
            {
                context.ReplyError("org.freedesktop.DBus.Error.UnknownProperty", "Unknown property.");
                return;
            }
            if (member is "Set")
            {
                context.ReplyError("org.freedesktop.DBus.Error.PropertyReadOnly", "Property is read-only.");
                return;
            }
            var reply = context.CreateReplyWriter("v");
            try
            {
                WriteProperty(ref reply, name);
                context.Reply(reply.CreateMessage());
            }
            finally { reply.Dispose(); }
        }
    }

    private sealed class ItemHandler(LinuxStatusNotifierTrayHandle owner) : Handler(SniObjectPath, SniInterface)
    {
        private static readonly string[] Properties = ["Category", "Id", "Title", "Status", "WindowId", "IconThemePath", "IconName", "IconPixmap", "OverlayIconName", "OverlayIconPixmap", "AttentionIconName", "AttentionIconPixmap", "AttentionMovieName", "ToolTip", "ItemIsMenu", "Menu"];
        private static readonly byte[] Xml =
            "<interface name=\"org.kde.StatusNotifierItem\"><property name=\"Category\" type=\"s\" access=\"read\"/><property name=\"Id\" type=\"s\" access=\"read\"/><property name=\"Title\" type=\"s\" access=\"read\"/><property name=\"Status\" type=\"s\" access=\"read\"/><property name=\"WindowId\" type=\"u\" access=\"read\"/><property name=\"IconThemePath\" type=\"s\" access=\"read\"/><property name=\"IconName\" type=\"s\" access=\"read\"/><property name=\"IconPixmap\" type=\"a(iiay)\" access=\"read\"/><property name=\"OverlayIconName\" type=\"s\" access=\"read\"/><property name=\"OverlayIconPixmap\" type=\"a(iiay)\" access=\"read\"/><property name=\"AttentionIconName\" type=\"s\" access=\"read\"/><property name=\"AttentionIconPixmap\" type=\"a(iiay)\" access=\"read\"/><property name=\"AttentionMovieName\" type=\"s\" access=\"read\"/><property name=\"ToolTip\" type=\"(sa(iiay)ss)\" access=\"read\"/><property name=\"ItemIsMenu\" type=\"b\" access=\"read\"/><property name=\"Menu\" type=\"o\" access=\"read\"/><method name=\"Activate\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"SecondaryActivate\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"ContextMenu\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"Scroll\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"s\"/></method><signal name=\"NewTitle\"/><signal name=\"NewToolTip\"/><signal name=\"NewStatus\"><arg type=\"s\"/></signal></interface>"u8.ToArray();
        protected override ReadOnlyMemory<byte> InterfaceXml => Xml;
        protected override string[] PropertyNames => Properties;

        protected override void WriteProperty(ref MessageWriter writer, string property)
        {
            switch (property)
            {
                case "Category": writer.WriteVariantString("ApplicationStatus"); break;
                case "Id": writer.WriteVariantString("CrossMacro"); break;
                case "Title": writer.WriteVariantString(owner.GetToolTipText() ?? string.Empty); break;
                case "Status": writer.WriteVariantString(owner.IsVisible ? "Active" : "Passive"); break;
                case "WindowId": writer.WriteVariantUInt32(0); break;
                case "ItemIsMenu": writer.WriteVariantBool(value: false); break;
                case "Menu": writer.WriteVariantObjectPath(MenuObjectPath); break;
                case "IconPixmap":
                    writer.WriteSignature("a(iiay)");
                    owner.WritePixmaps(ref writer);
                    break;
                case "OverlayIconPixmap":
                case "AttentionIconPixmap":
                    writer.WriteSignature("a(iiay)");
                    var empty = writer.WriteArrayStart(DBusType.Struct);
                    writer.WriteArrayEnd(empty);
                    break;
                case "ToolTip":
                    writer.WriteSignature("(sa(iiay)ss)");
                    writer.WriteStructureStart();
                    writer.WriteString(string.Empty);
                    owner.WritePixmaps(ref writer);
                    writer.WriteString(owner.GetToolTipText() ?? string.Empty);
                    writer.WriteString(string.Empty);
                    break;
                default: writer.WriteVariantString(string.Empty); break;
            }
        }

        protected override ValueTask DispatchAsync(MethodContext context)
        {
            var member = context.Request.MemberAsString;
            if (member is not ("Activate" or "SecondaryActivate" or "ContextMenu" or "Scroll"))
            {
                context.ReplyUnknownMethodError();
                return ValueTask.CompletedTask;
            }
            if (CheckSignature(context, member is "Scroll" ? "is" : "ii"))
            {
                if (member is "Activate") { owner.Activate(); }
                ReplyEmpty(context);
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MenuHandler : Handler, IDisposable
    {
        private static readonly string[] Properties = ["Version", "TextDirection", "Status", "IconThemePath"];
        private static readonly byte[] Xml =
            "<interface name=\"com.canonical.dbusmenu\"><property name=\"Version\" type=\"u\" access=\"read\"/><property name=\"TextDirection\" type=\"s\" access=\"read\"/><property name=\"Status\" type=\"s\" access=\"read\"/><property name=\"IconThemePath\" type=\"as\" access=\"read\"/><method name=\"GetLayout\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"as\"/><arg direction=\"out\" type=\"u\"/><arg direction=\"out\" type=\"(ia{sv}av)\"/></method><method name=\"GetGroupProperties\"><arg direction=\"in\" type=\"ai\"/><arg direction=\"in\" type=\"as\"/><arg direction=\"out\" type=\"a(ia{sv})\"/></method><method name=\"GetProperty\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"s\"/><arg direction=\"out\" type=\"v\"/></method><method name=\"Event\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"s\"/><arg direction=\"in\" type=\"v\"/><arg direction=\"in\" type=\"u\"/></method><method name=\"EventGroup\"><arg direction=\"in\" type=\"a(isvu)\"/><arg direction=\"out\" type=\"ai\"/></method><method name=\"AboutToShow\"><arg direction=\"in\" type=\"i\"/><arg direction=\"out\" type=\"b\"/></method><method name=\"AboutToShowGroup\"><arg direction=\"in\" type=\"ai\"/><arg direction=\"out\" type=\"ai\"/><arg direction=\"out\" type=\"ai\"/></method><signal name=\"LayoutUpdated\"><arg type=\"u\"/><arg type=\"i\"/></signal></interface>"u8.ToArray();
        private readonly LinuxStatusNotifierTrayHandle _owner;
        private readonly NativeMenu _menu;
        private readonly Dictionary<int, NativeMenuItemBase> _items = [];
        private readonly Dictionary<NativeMenuItemBase, int> _ids = [];
        private readonly HashSet<NativeMenu> _menus = [];
        private int _nextId = 1;
        private uint _revision = 1;
        private bool _disposed;

        public MenuHandler(LinuxStatusNotifierTrayHandle owner, NativeMenu menu) : base(MenuObjectPath, MenuInterface)
        {
            _owner = owner;
            _menu = menu;
            Rebuild(menu, previous: null);
        }
        protected override ReadOnlyMemory<byte> InterfaceXml => Xml;
        protected override string[] PropertyNames => Properties;


        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            Detach();
        }

        private void Detach()
        {
            foreach (var menu in _menus)
            {
                if (menu.Items is INotifyCollectionChanged collection) { collection.CollectionChanged -= OnCollectionChanged; }
            }
            foreach (var item in _ids.Keys) { item.PropertyChanged -= OnPropertyChanged; }
            _menus.Clear();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Changed(rebuild: true);
        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args) =>
            Changed(rebuild: args.Property == NativeMenuItem.MenuProperty);

        private void Changed(bool rebuild)
        {
            if (_disposed) { return; }
            if (rebuild)
            {
                // Retain IDs for surviving items but release removed items and their event subscriptions.
                Detach();
                var previous = new Dictionary<NativeMenuItemBase, int>(_ids);
                _ids.Clear();
                _items.Clear();
                Rebuild(_menu, previous);
            }
            unchecked { _revision++; }
            lock (_owner._lock)
            {
                if (_owner._connection is not { } connection || _owner._isDisposed) { return; }
                try
                {
                    using var writer = connection.GetMessageWriter();
                    writer.WriteSignalHeader(path: MenuObjectPath, @interface: MenuInterface, member: "LayoutUpdated", signature: "ui");
                    writer.WriteUInt32(_revision);
                    writer.WriteInt32(0);
                    if (!connection.TrySendMessage(writer.CreateMessage()))
                    {
                        Serilog.Log.Debug("[LinuxTray] Cannot emit LayoutUpdated: session bus is not connected.");
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Serilog.Log.Debug(ex, "[LinuxTray] Failed to emit LayoutUpdated");
                }
            }
        }

        private void Rebuild(NativeMenu menu, Dictionary<NativeMenuItemBase, int>? previous)
        {
            if (!_menus.Add(menu)) { return; }
            if (menu.Items is INotifyCollectionChanged collection) { collection.CollectionChanged += OnCollectionChanged; }
            foreach (var item in menu.Items)
            {
                if (_ids.ContainsKey(item)) { continue; }
                var id = previous is not null && previous.TryGetValue(item, out var oldId) ? oldId : _nextId++;
                _ids.Add(item, id);
                _items.Add(id, item);
                item.PropertyChanged += OnPropertyChanged;
                if (item is NativeMenuItem { Menu: { } child }) { Rebuild(child, previous); }
            }
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

        private void DispatchOnUIThread(MethodContext context)
        {
            if (_disposed || _owner._isDisposed)
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
                case "GetLayout":
                    var parent = reader.ReadInt32();
                    var depth = reader.ReadInt32();
                    var names = reader.ReadArrayOfString();
                    if (!Contains(parent)) { ReplyInvalidId(context); return; }
                    if (depth < -1) { context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Invalid recursion depth."); return; }
                    var layoutWriter = context.CreateReplyWriter("u(ia{sv}av)");
                    try
                    {
                        layoutWriter.WriteUInt32(_revision);
                        WriteLayout(ref layoutWriter, parent, depth, names);
                        context.Reply(layoutWriter.CreateMessage());
                    }
                    finally { layoutWriter.Dispose(); }
                    break;
                case "GetGroupProperties":
                    var ids = reader.ReadArrayOfInt32();
                    var properties = reader.ReadArrayOfString();
                    var groupWriter = context.CreateReplyWriter("a(ia{sv})");
                    try
                    {
                        var array = groupWriter.WriteArrayStart(DBusType.Struct);
                        if (ids.Length is 0)
                        {
                            WriteGroupItem(ref groupWriter, 0, properties);
                            foreach (var id in _items.Keys) { WriteGroupItem(ref groupWriter, id, properties); }
                        }
                        else { foreach (var id in ids) { if (Contains(id)) { WriteGroupItem(ref groupWriter, id, properties); } } }
                        groupWriter.WriteArrayEnd(array);
                        context.Reply(groupWriter.CreateMessage());
                    }
                    finally { groupWriter.Dispose(); }
                    break;
                case "GetProperty":
                    var itemId = reader.ReadInt32();
                    var name = reader.ReadString();
                    if (!Contains(itemId)) { ReplyInvalidId(context); return; }
                    if (!TryGetItemProperty(itemId, name, out var value))
                    {
                        context.ReplyError("com.canonical.dbusmenu.Error.InvalidProperty", "Unknown menu item property.");
                        return;
                    }
                    using (var writer = context.CreateReplyWriter("v")) { writer.WriteVariant(value); context.Reply(writer.CreateMessage()); }
                    break;
                case "Event":
                    var eventItem = reader.ReadInt32();
                    var eventName = reader.ReadString();
                    _ = reader.ReadVariantValue();
                    _ = reader.ReadUInt32();
                    if (!Contains(eventItem)) { ReplyInvalidId(context); return; }
                    TriggerEvent(eventItem, eventName);
                    ReplyEmpty(context);
                    break;
                case "EventGroup":
                    var errors = new List<int>();
                    var events = reader.ReadArrayStart(DBusType.Struct);
                    while (reader.HasNext(events))
                    {
                        reader.AlignStruct();
                        var id = reader.ReadInt32();
                        var eventId = reader.ReadString();
                        _ = reader.ReadVariantValue();
                        _ = reader.ReadUInt32();
                        if (Contains(id)) { TriggerEvent(id, eventId); } else { errors.Add(id); }
                    }
                    using (var writer = context.CreateReplyWriter("ai")) { writer.WriteArray(errors); context.Reply(writer.CreateMessage()); }
                    break;
                case "AboutToShow":
                    if (!Contains(reader.ReadInt32())) { ReplyInvalidId(context); return; }
                    using (var writer = context.CreateReplyWriter("b")) { writer.WriteBool(value: false); context.Reply(writer.CreateMessage()); }
                    break;
                case "AboutToShowGroup":
                    var requested = reader.ReadArrayOfInt32();
                    var invalid = requested.Where(id => !Contains(id)).ToList();
                    using (var writer = context.CreateReplyWriter("aiai"))
                    {
                        writer.WriteArray(Array.Empty<int>());
                        writer.WriteArray(invalid);
                        context.Reply(writer.CreateMessage());
                    }
                    break;
            }
        }

        private bool Contains(int id) => id is 0 || _items.ContainsKey(id);
        private static void ReplyInvalidId(MethodContext context) => context.ReplyError("com.canonical.dbusmenu.Error.InvalidMenuItem", "Unknown menu item.");

        private void TriggerEvent(int id, string eventName)
        {
            if (!string.Equals(eventName, "clicked", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(eventName, "activate", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_items.TryGetValue(id, out var item)
                && item is NativeMenuItem { IsEnabled: true, IsVisible: true, Menu: null } menuItem)
            {
                if (menuItem is INativeMenuItemExporterEventsImplBridge bridge)
                {
                    bridge.RaiseClicked();
                }
                else if (menuItem.Command is { } command && command.CanExecute(menuItem.CommandParameter))
                {
                    command.Execute(menuItem.CommandParameter);
                }
            }
        }

        private void WriteGroupItem(ref MessageWriter writer, int id, string[] names)
        {
            writer.WriteStructureStart();
            writer.WriteInt32(id);
            WriteItemProperties(ref writer, id, names);
        }

        private void WriteLayout(ref MessageWriter writer, int id, int depth, string[] names)
        {
            writer.WriteStructureStart();
            writer.WriteInt32(id);
            WriteItemProperties(ref writer, id, names);
            var children = writer.WriteArrayStart(DBusType.Variant);
            var menu = id is 0 ? _menu : (_items[id] as NativeMenuItem)?.Menu;
            if (depth is not 0 && menu is not null)
            {
                foreach (var item in menu.Items)
                {
                    writer.WriteSignature("(ia{sv}av)");
                    WriteLayout(ref writer, _ids[item], depth < 0 ? -1 : depth - 1, names);
                }
            }
            writer.WriteArrayEnd(children);
        }

        private static readonly string[] ItemPropertyNames = ["type", "label", "enabled", "visible", "children-display", "toggle-type", "toggle-state"];
        private void WriteItemProperties(ref MessageWriter writer, int id, string[] names)
        {
            var array = writer.WriteDictionaryStart();
            foreach (var name in names.Length is 0 ? ItemPropertyNames : names)
            {
                if (!TryGetItemProperty(id, name, out var value)) { continue; }
                writer.WriteDictionaryEntryStart();
                writer.WriteString(name);
                writer.WriteVariant(value);
            }
            writer.WriteDictionaryEnd(array);
        }

        private bool TryGetItemProperty(int id, string name, out VariantValue value)
        {
            value = default;
            _ = _items.TryGetValue(id, out var item);
            var menuItem = item as NativeMenuItem;
            switch (name)
            {
                case "type": value = VariantValue.String(item is NativeMenuItemSeparator ? "separator" : "standard"); return true;
                case "label" when menuItem is not null: value = VariantValue.String(menuItem.Header ?? string.Empty); return true;
                case "enabled": value = VariantValue.Bool(menuItem?.IsEnabled ?? true); return true;
                case "visible": value = VariantValue.Bool(menuItem?.IsVisible ?? true); return true;
                case "children-display" when id is 0 || menuItem?.Menu is not null: value = VariantValue.String("submenu"); return true;
                case "toggle-type" when menuItem?.ToggleType is MenuItemToggleType.CheckBox: value = VariantValue.String("checkmark"); return true;
                case "toggle-type" when menuItem?.ToggleType is MenuItemToggleType.Radio: value = VariantValue.String("radio"); return true;
                case "toggle-state" when menuItem is not null && menuItem.ToggleType is not MenuItemToggleType.None: value = VariantValue.Int32(menuItem.IsChecked ? 1 : 0); return true;
                default: return false;
            }
        }
    }
}
