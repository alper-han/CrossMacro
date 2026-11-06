using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using CrossMacro.UI.Linux.Services;
using Tmds.DBus.Protocol;
using Xunit;

namespace CrossMacro.UI.Tests.Services;

[Collection("Headless startup")]
public sealed class LinuxStatusNotifierTrayBackendTests
{
    private const string ItemPath = "/StatusNotifierItem";
    private const string MenuPath = "/StatusNotifierMenu";
    private const string ItemInterface = "org.kde.StatusNotifierItem";
    private const string MenuInterface = "com.canonical.dbusmenu";
    private const string WatcherName = "org.kde.StatusNotifierWatcher";

    public static bool CanRunBusTests => OperatingSystem.IsLinux()
        && string.Equals(Environment.GetEnvironmentVariable("CROSSMACRO_DBUS_INTEGRATION_TESTS"), "1", StringComparison.Ordinal)
        && (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator)
            .Any(directory => File.Exists(Path.Combine(directory, "dbus-daemon")));

    [Fact]
    public Task Handle_WithoutBus_PreservesStateAndDisposesIdempotently() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(_ =>
        {
            // Empty explicitly disables D-Bus. Null means use the real session bus.
            using var handle = new LinuxStatusNotifierTrayHandle(string.Empty, new NativeMenu(), "CrossMacro ToolTip",
                (1, 1, [128, 10, 20, 30]));
            Assert.True(handle.HasNativeMenuExporter);
            Assert.True(handle.IsVisible);
            Assert.Equal("CrossMacro ToolTip", handle.GetToolTipText());
            handle.SetToolTipText("Updated ToolTip");
            handle.IsVisible = false;
            Assert.Equal("Updated ToolTip", handle.GetToolTipText());
            Assert.False(handle.IsVisible);
            handle.Dispose();
            handle.Dispose();
            handle.SetToolTipText("After disposal");
            handle.IsVisible = true;
            Assert.Equal("Updated ToolTip", handle.GetToolTipText());
            Assert.False(handle.IsVisible);
            return Task.CompletedTask;
        });

    [Fact]
    public Task Backend_InvalidIconBytes_FailsInsteadOfExportingPlaceholder() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(_ =>
        {
            using var stream = new MemoryStream([0]);
            // Avalonia.Headless accepts the stream but its icon Save implementation writes no bytes.
            var icon = new WindowIcon(stream);
            var backend = new LinuxStatusNotifierTrayBackend(string.Empty);
            Assert.Throws<InvalidDataException>(() => backend.Create(icon, new NativeMenu(), "Test"));
            return Task.CompletedTask;
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task ItemProperties_HaveSniWireTypesAndIntrospection() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var bus = await Session.CreateAsync(new NativeMenu(), token);
            var properties = await bus.GetAllAsync(ItemPath, ItemInterface, token);
            Assert.Equal("CrossMacro Test", properties["Title"].GetString());
            Assert.Equal(VariantValueType.UInt32, properties["WindowId"].Type);
            Assert.Equal(MenuPath, properties["Menu"].GetObjectPathAsString());
            Assert.Equal(VariantValueType.Struct, properties["IconPixmap"].ItemType);
            var pixmap = properties["IconPixmap"].GetItem(0);
            Assert.Equal(1, pixmap.GetItem(0).GetInt32());
            Assert.Equal(1, pixmap.GetItem(1).GetInt32());
            Assert.Equal(new byte[] { 128, 10, 20, 30 }, pixmap.GetItem(2).GetArray<byte>());
            Assert.Equal(VariantValueType.Struct, properties["ToolTip"].Type);
            Assert.Equal(VariantValueType.Struct, properties["ToolTip"].GetItem(1).ItemType);
            Assert.Equal("CrossMacro Test", properties["ToolTip"].GetItem(2).GetString());

            Assert.Equal("a(iiay)", await bus.GetPropertySignatureAsync("IconPixmap", token));
            Assert.Equal("(sa(iiay)ss)", await bus.GetPropertySignatureAsync("ToolTip", token));
            foreach (var (path, iface) in new[] { (ItemPath, ItemInterface), (MenuPath, MenuInterface) })
            {
                var xml = await bus.CallAsync(path: path, iface: "org.freedesktop.DBus.Introspectable", member: "Introspect", signature: null, body: null,
                    static (message, _) => message.GetBodyReader().ReadString(), token);
                var introspection = XDocument.Parse(xml);
                Assert.Contains(introspection.Root!.Elements("interface"), element => string.Equals((string?)element.Attribute("name"), iface, StringComparison.Ordinal));
                if (string.Equals(iface, ItemInterface, StringComparison.Ordinal))
                {
                    var item = introspection.Root.Elements("interface").Single(element => string.Equals((string?)element.Attribute("name"), iface, StringComparison.Ordinal));
                    Assert.Equal("a(iiay)", (string?)item.Elements("property").Single(element => string.Equals((string?)element.Attribute("name"), "IconPixmap", StringComparison.Ordinal)).Attribute("type"));
                }
            }
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task Layout_HonorsParentDepthFiltersAndLiveChanges() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var submenu = new NativeMenu();
            var child = new NativeMenuItem { Header = "Nested" };
            submenu.Add(child);
            var parent = new NativeMenuItem { Header = "Parent", Menu = submenu };
            var menu = new NativeMenu { parent, new NativeMenuItemSeparator() };
            await using var bus = await Session.CreateAsync(menu, token);
            var initial = await bus.GetLayoutAsync(0, -1, [], token);
            Assert.Equal(2, initial.Children.Count);
            var parentLayout = initial.Children[0];
            Assert.Single(parentLayout.Children);
            Assert.Equal("Nested", parentLayout.Children[0].Properties["label"].GetString());
            Assert.Equal("separator", initial.Children[1].Properties["type"].GetString());
            var rootOnly = await bus.GetLayoutAsync(0, 0, [], token);
            Assert.Empty(rootOnly.Children);
            var shallow = await bus.GetLayoutAsync(parentLayout.Id, 0, ["label"], token);
            Assert.Equal(parentLayout.Id, shallow.Id);
            Assert.Equal("Parent", shallow.Properties["label"].GetString());
            Assert.Single(shallow.Properties);
            Assert.Empty(shallow.Children);

            var changed = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = await bus.Client.AddMatchAsync(
                new MatchRule { Type = MessageType.Signal, Sender = bus.Destination, Path = MenuPath, Interface = MenuInterface, Member = "LayoutUpdated" },
                static (message, _) =>
                {
                    Assert.Equal("ui", message.SignatureAsString);
                    var reader = message.GetBodyReader();
                    var revision = reader.ReadUInt32();
                    Assert.Equal(0, reader.ReadInt32());
                    return revision;
                },
                notification =>
                {
                    if (notification.IsCompletion) { changed.TrySetException(notification.CompletionReason); }
                    else if (notification.HasValue) { changed.TrySetResult(notification.Value); }
                }, emitOnCapturedContext: false);
            child.Header = "Renamed";
            Assert.True(await changed.Task.WaitAsync(token) > initial.Revision);
            var updated = await bus.GetLayoutAsync(parentLayout.Id, -1, [], token);
            Assert.Equal(parentLayout.Children[0].Id, updated.Children[0].Id);
            Assert.Equal("Renamed", updated.Children[0].Properties["label"].GetString());
            submenu.Items.Remove(child);
            var removed = await bus.GetLayoutAsync(parentLayout.Id, -1, [], token);
            Assert.Empty(removed.Children);
            await bus.ExpectErrorAsync(MenuPath, MenuInterface, "GetProperty", "is", (ref MessageWriter writer) =>
            {
                writer.WriteInt32(parentLayout.Children[0].Id);
                writer.WriteString("label");
            }, "com.canonical.dbusmenu.Error.InvalidMenuItem", token);
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task ActivationAndMenuEvents_DispatchRealCallsAndRejectDisabledItems() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            var clicks = 0;
            var item = new NativeMenuItem { Header = "Run" };
            item.Click += (_, _) => clicks++;
            var menu = new NativeMenu { item };
            await using var bus = await Session.CreateAsync(menu, token);
            var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bus.Handle.Clicked += (_, _) => activated.TrySetResult();
            await bus.CallEmptyAsync(ItemPath, ItemInterface, "Activate", "ii", static (ref MessageWriter writer) =>
            {
                writer.WriteInt32(100);
                writer.WriteInt32(200);
            }, token);
            await activated.Task.WaitAsync(token);
            var layout = await bus.GetLayoutAsync(0, -1, [], token);
            var id = layout.Children[0].Id;
            await bus.ClickAsync(id, token);
            Assert.Equal(1, clicks);
            item.IsEnabled = false;
            await bus.ClickAsync(id, token);
            Assert.Equal(1, clicks);
            item.IsEnabled = true;
            item.IsVisible = false;
            await bus.ClickAsync(id, token);
            Assert.Equal(1, clicks);
            item.IsVisible = true;
            var errors = await bus.CallAsync(MenuPath, MenuInterface, "EventGroup", "a(isvu)", (ref MessageWriter writer) =>
            {
                var array = writer.WriteArrayStart(DBusType.Struct);
                foreach (var eventId in new[] { id, int.MaxValue })
                {
                    writer.WriteStructureStart();
                    writer.WriteInt32(eventId);
                    writer.WriteString("clicked");
                    writer.WriteVariantInt32(0);
                    writer.WriteUInt32(0);
                }
                writer.WriteArrayEnd(array);
            }, static (message, _) => message.GetBodyReader().ReadArrayOfInt32(), token);
            Assert.Equal([int.MaxValue], errors);
            Assert.Equal(2, clicks);
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task PropertiesAndMethods_RejectInvalidContracts() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var bus = await Session.CreateAsync(new NativeMenu(), token);
            await bus.ExpectErrorAsync(ItemPath, ItemInterface, "Activate", "s", static (ref MessageWriter writer) => writer.WriteString("wrong"),
                "org.freedesktop.DBus.Error.InvalidArgs", token);
            await bus.ExpectErrorAsync(ItemPath, "wrong.interface", "Activate", "ii", static (ref MessageWriter writer) =>
            {
                writer.WriteInt32(0); writer.WriteInt32(0);
            }, "org.freedesktop.DBus.Error.UnknownMethod", token);
            await bus.ExpectErrorAsync(ItemPath, "org.freedesktop.DBus.Properties", "Get", "ss", static (ref MessageWriter writer) =>
            {
                writer.WriteString(ItemInterface); writer.WriteString("NoSuchProperty");
            }, "org.freedesktop.DBus.Error.UnknownProperty", token);
            await bus.ExpectErrorAsync(MenuPath, "org.freedesktop.DBus.Properties", "GetAll", "s", static (ref MessageWriter writer) => writer.WriteString(ItemInterface),
                "org.freedesktop.DBus.Error.UnknownInterface", token);
            var menuProperties = await bus.GetAllAsync(MenuPath, MenuInterface, token);
            Assert.Equal(4u, menuProperties["Version"].GetUInt32());
            Assert.Empty(menuProperties["IconThemePath"].GetArray<string>());
            await bus.ExpectErrorAsync(MenuPath, "org.freedesktop.DBus.Properties", "Set", "ssv", static (ref MessageWriter writer) =>
            {
                writer.WriteString(MenuInterface); writer.WriteString("Status"); writer.WriteVariantString("notice");
            }, "org.freedesktop.DBus.Error.PropertyReadOnly", token);
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task WatcherRestart_ReregistersAndDisposedHandlesCannotRegisterAgain() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var bus = await Session.CreateAsync(new NativeMenu(), token);
            await bus.Client.ReleaseNameAsync(WatcherName).WaitAsync(token);
            using var replacement = new DBusConnection(bus.Address);
            await replacement.ConnectAsync();
            var watcher = new Watcher();
            replacement.AddMethodHandler(watcher);
            await replacement.RequestNameAsync(WatcherName).WaitAsync(token);
            var registration = await watcher.Registrations.Reader.ReadAsync(token);
            Assert.Equal(bus.Destination, registration.Sender);
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = await bus.Client.AddMatchAsync(
                new MatchRule { Type = MessageType.Signal, Sender = "org.freedesktop.DBus", Path = "/org/freedesktop/DBus", Interface = "org.freedesktop.DBus", Member = "NameOwnerChanged" },
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    return (Name: reader.ReadString(), OldOwner: reader.ReadString(), NewOwner: reader.ReadString());
                },
                notification =>
                {
                    if (notification.IsCompletion) { disconnected.TrySetException(notification.CompletionReason); }
                    else if (notification.HasValue && string.Equals(notification.Value.Name, bus.Destination, StringComparison.Ordinal) && notification.Value.NewOwner.Length is 0)
                    {
                        disconnected.TrySetResult();
                    }
                }, emitOnCapturedContext: false);
            bus.Handle.Dispose();
            bus.Handle.Dispose();
            await disconnected.Task.WaitAsync(token);
            await replacement.ReleaseNameAsync(WatcherName).WaitAsync(token);
            Assert.False(watcher.Registrations.Reader.TryRead(out _));
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task RejectedRegistration_DoesNotReportReady() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var bus = await Session.CreateAsync(new NativeMenu(), token, rejectRegistration: true);

            Assert.True(bus.Handle.HasNativeMenuExporter);
            Assert.False(bus.Handle.IsReady);
        });

    [Fact(SkipUnless = nameof(CanRunBusTests), Skip = "Private bus integration tests require CROSSMACRO_DBUS_INTEGRATION_TESTS=1 and dbus-daemon.")]
    public Task WatcherLossAndReturn_PublishesRegistrationAvailability() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(async token =>
        {
            await using var bus = await Session.CreateAsync(new NativeMenu(), token);
            Assert.True(bus.Handle.IsReady);
            var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bus.Handle.AvailabilityChanged += (_, _) =>
            {
                if (bus.Handle.IsReady && lost.Task.IsCompleted) { restored.TrySetResult(); }
                else if (!bus.Handle.IsReady) { lost.TrySetResult(); }
            };

            await bus.Client.ReleaseNameAsync(WatcherName).WaitAsync(token);
            await lost.Task.WaitAsync(token);
            Assert.False(bus.Handle.IsReady);

            using var replacement = new DBusConnection(bus.Address);
            await replacement.ConnectAsync();
            replacement.AddMethodHandler(new Watcher());
            await replacement.RequestNameAsync(WatcherName).WaitAsync(token);
            await restored.Task.WaitAsync(token);
            Assert.True(bus.Handle.IsReady);
        });

    private delegate void WriteBody(ref MessageWriter writer);
    private sealed record Layout(uint Revision, int Id, Dictionary<string, VariantValue> Properties, List<Layout> Children);
    private sealed record Registration(string Sender, string Path);

    private sealed class Watcher(bool rejectRegistration = false) : IPathMethodHandler
    {
        public Channel<Registration> Registrations { get; } = Channel.CreateUnbounded<Registration>();
        public string Path => "/StatusNotifierWatcher";
        public bool HandlesChildPaths => false;
        public ValueTask HandleMethodAsync(MethodContext context)
        {
            Assert.Equal(WatcherName, context.Request.InterfaceAsString);
            Assert.Equal("RegisterStatusNotifierItem", context.Request.MemberAsString);
            Assert.Equal("s", context.Request.SignatureAsString);
            var path = context.Request.GetBodyReader().ReadString();
            if (rejectRegistration)
            {
                context.ReplyError("org.freedesktop.DBus.Error.AccessDenied", "Registration rejected.");
            }
            else
            {
                using var writer = context.CreateReplyWriter(signature: null);
                context.Reply(writer.CreateMessage());
            }
            Assert.True(Registrations.Writer.TryWrite(new Registration(context.Request.SenderAsString!, path)));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Session(Process daemon, DirectoryInfo directory, string address, DBusConnection client, LinuxStatusNotifierTrayHandle handle, string destination) : IAsyncDisposable
    {
        public string Address => address;
        public DBusConnection Client => client;
        public LinuxStatusNotifierTrayHandle Handle => handle;
        public string Destination => destination;

        public static async Task<Session> CreateAsync(NativeMenu menu, CancellationToken token, bool rejectRegistration = false)
        {
            var directory = Directory.CreateTempSubdirectory("CrossMacroTrayBus_");
            var start = new ProcessStartInfo("dbus-daemon") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add("--session");
            start.ArgumentList.Add("--nofork");
            start.ArgumentList.Add("--print-address=1");
            start.ArgumentList.Add($"--address=unix:tmpdir={directory.FullName}");
            var daemon = Process.Start(start) ?? throw new InvalidOperationException("Cannot start private D-Bus daemon.");
            try
            {
                var address = await daemon.StandardOutput.ReadLineAsync(token)
                    ?? throw new InvalidOperationException("Private D-Bus daemon did not provide an address.");
                var client = new DBusConnection(address);
                try
                {
                    await client.ConnectAsync();
                    var watcher = new Watcher(rejectRegistration);
                    client.AddMethodHandler(watcher);
                    var handle = new LinuxStatusNotifierTrayHandle(address, menu, "CrossMacro Test", (1, 1, [128, 10, 20, 30]));
                    try
                    {
                        await client.RequestNameAsync(WatcherName).WaitAsync(token);
                        var registration = await watcher.Registrations.Reader.ReadAsync(token);
                        Assert.Equal(ItemPath, registration.Path);
                        await handle.WaitForReadyAsync(token);
                        return new Session(daemon, directory, address, client, handle, registration.Sender);
                    }
                    catch
                    {
                        handle.Dispose();
                        throw;
                    }
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            }
            catch
            {
                await StopAsync(daemon);
                directory.Delete(recursive: true);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            handle.Dispose();
            client.Dispose();
            await StopAsync(daemon);
            directory.Delete(recursive: true);
        }

        private static async Task StopAsync(Process process)
        {
            try
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
            }
            finally { process.Dispose(); }
        }

        public async Task<T> CallAsync<T>(string path, string iface, string member, string? signature, WriteBody? body, MessageValueReader<T> read, CancellationToken token)
        {
            MessageBuffer message;
            var writer = client.GetMessageWriter();
            try
            {
                writer.WriteMethodCallHeader(destination: destination, path: path, @interface: iface, member: member, signature: signature);
                body?.Invoke(ref writer);
                message = writer.CreateMessage();
            }
            finally { writer.Dispose(); }
            return await client.CallMethodAsync(message, read).WaitAsync(token);
        }

        public Task CallEmptyAsync(string path, string iface, string member, string? signature, WriteBody? body, CancellationToken token) =>
            CallAsync(path, iface, member, signature, body, static (message, _) =>
            {
                Assert.True(string.IsNullOrEmpty(message.SignatureAsString));
                return true;
            }, token);

        public async Task ExpectErrorAsync(string path, string iface, string member, string signature, WriteBody body, string errorName, CancellationToken token)
        {
            var exception = await Assert.ThrowsAsync<DBusErrorReplyException>(() => CallEmptyAsync(path, iface, member, signature, body, token));
            Assert.Equal(errorName, exception.ErrorName);
        }

        public Task<Dictionary<string, VariantValue>> GetAllAsync(string path, string iface, CancellationToken token) =>
            CallAsync(path, "org.freedesktop.DBus.Properties", "GetAll", "s", (ref MessageWriter writer) => writer.WriteString(iface),
                static (message, _) => message.GetBodyReader().ReadDictionaryOfStringToVariantValue(), token);

        public Task<string> GetPropertySignatureAsync(string name, CancellationToken token) =>
            CallAsync(ItemPath, "org.freedesktop.DBus.Properties", "Get", "ss", (ref MessageWriter writer) =>
            {
                writer.WriteString(ItemInterface); writer.WriteString(name);
            }, static (message, _) => message.GetBodyReader().ReadSignatureAsString(), token);

        public Task<Layout> GetLayoutAsync(int parent, int depth, string[] properties, CancellationToken token) =>
            CallAsync(MenuPath, MenuInterface, "GetLayout", "iias", (ref MessageWriter writer) =>
            {
                writer.WriteInt32(parent); writer.WriteInt32(depth); writer.WriteArray(properties);
            }, static (message, _) =>
            {
                Assert.Equal("u(ia{sv}av)", message.SignatureAsString);
                var reader = message.GetBodyReader();
                var revision = reader.ReadUInt32();
                return ReadLayout(ref reader, revision);
            }, token);

        private static Layout ReadLayout(ref Reader reader, uint revision)
        {
            reader.AlignStruct();
            var id = reader.ReadInt32();
            var properties = reader.ReadDictionaryOfStringToVariantValue();
            var children = new List<Layout>();
            var array = reader.ReadArrayStart(DBusType.Variant);
            while (reader.HasNext(array))
            {
                Assert.Equal("(ia{sv}av)", reader.ReadSignatureAsString());
                children.Add(ReadLayout(ref reader, revision));
            }
            return new Layout(revision, id, properties, children);
        }

        public Task ClickAsync(int id, CancellationToken token) =>
            CallEmptyAsync(MenuPath, MenuInterface, "Event", "isvu", (ref MessageWriter writer) =>
            {
                writer.WriteInt32(id); writer.WriteString("clicked"); writer.WriteVariantInt32(0); writer.WriteUInt32(0);
            }, token);
    }
}
