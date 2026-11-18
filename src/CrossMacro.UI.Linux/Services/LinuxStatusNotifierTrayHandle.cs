using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CrossMacro.Core.Logging;
using CrossMacro.UI.Services.Desktop;
using Tmds.DBus.Protocol;

namespace CrossMacro.UI.Linux.Services;

internal sealed class LinuxStatusNotifierTrayHandle : ITrayIconHandle
{
    internal const string SniObjectPath = "/StatusNotifierItem";
    internal const string MenuObjectPath = "/StatusNotifierMenu";
    internal const string SniInterface = "org.kde.StatusNotifierItem";
    internal const string MenuInterface = "com.canonical.dbusmenu";
    private const string WatcherBusName = "org.kde.StatusNotifierWatcher";
    private const string WatcherObjectPath = "/StatusNotifierWatcher";
    private static readonly TimeSpan InitialRegistrationTimeout = TimeSpan.FromSeconds(2);
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource _initialRegistration = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DBusMenuHandler _menuHandler;
    private readonly StatusNotifierItemHandler _itemHandler;
    private readonly int _iconWidth;
    private readonly int _iconHeight;
    private readonly byte[] _iconPixmap;
    private DBusConnection? _connection;
    private bool _isDisposed;
    private bool _isRegistered;
    public event EventHandler? Clicked;
    public event EventHandler? AvailabilityChanged;

    public LinuxStatusNotifierTrayHandle(string? sessionBusAddress, NativeMenu menu, string toolTipText,
        (int Width, int Height, byte[] Pixmap) icon)
    {
        ArgumentNullException.ThrowIfNull(menu);
        Dispatcher.UIThread.VerifyAccess();
        ToolTipText = toolTipText;
        (_iconWidth, _iconHeight, _iconPixmap) = icon;
        _itemHandler = new StatusNotifierItemHandler(this);
        _menuHandler = new DBusMenuHandler(this, menu);
        if (!string.IsNullOrWhiteSpace(sessionBusAddress))
        {
            _ = RunBusAsync(sessionBusAddress, _shutdown.Token);
        }
        else
        {
            _ = _initialRegistration.TrySetResult();
        }
    }

    public bool IsVisible
    {
        get { lock (_lock) { return field; } }
        set
        {
            lock (_lock)
            {
                if (_isDisposed || field == value) { return; }
                field = value;
                EmitSignal(SniObjectPath, SniInterface, "NewStatus", value ? "Active" : "Passive");
            }
        }
    } = true;

    internal bool IsDisposed { get { lock (_lock) { return _isDisposed; } } }

    public bool HasNativeMenuExporter { get { lock (_lock) { return !_isDisposed; } } }
    public bool IsReady { get { lock (_lock) { return _isRegistered && !_isDisposed; } } }

    public async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _initialRegistration.Task.WaitAsync(InitialRegistrationTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A missing watcher must not block desktop startup indefinitely.
        }
    }

    private void SetRegistered(bool registered)
    {
        lock (_lock)
        {
            registered &= !_isDisposed;
            if (_isRegistered == registered) { return; }
            _isRegistered = registered;
        }
        Dispatcher.UIThread.Post(() => AvailabilityChanged?.Invoke(this, EventArgs.Empty));
    }

    internal string ToolTipText { get; }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) { return; }
            _isDisposed = true;
            _shutdown.Cancel();
            _connection?.Dispose();
            _connection = null;
            _shutdown.Dispose();
        }
        SetRegistered(registered: false);
        _ = _initialRegistration.TrySetResult();
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
                    ownerChanged.Token.ThrowIfCancellationRequested();
                    SetRegistered(registered: true);
                    _ = _initialRegistration.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, ownerChanged.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ownerChanged.IsCancellationRequested)
                {
                    // Register again when a different watcher owns the name.
                }
                catch (DBusOwnerChangedException)
                {
                    // Watcher owner changed while registration was in-flight.
                }
                catch (DBusErrorReplyException ex)
                {
                    Log.Warning(ex, "[LinuxTray] StatusNotifierItem registration was rejected.");
                    _ = _initialRegistration.TrySetResult();
                    // Do not spin on a rejecting host; wait for its owner to change.
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, ownerChanged.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (ownerChanged.IsCancellationRequested) { /* Ignored */ }
                }
                finally
                {
                    SetRegistered(registered: false);
                }
            }
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is not OutOfMemoryException)
        {
            // Disposing the connection interrupts pending connection and bus operations.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[LinuxTray] Session bus connection or watcher monitor failed.");
        }
        finally
        {
            SetRegistered(registered: false);
            _ = _initialRegistration.TrySetResult();
            lock (_lock)
            {
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
        Log.Information("[LinuxTray] StatusNotifierItem registered with host watcher.");
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
                    Log.Debug("[LinuxTray] Cannot emit {Member}: session bus is not connected.", member);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[LinuxTray] Failed to emit {Member}", member);
            }
        }
    }
    internal void EmitLayoutUpdated(uint revision)
    {
        lock (_lock)
        {
            if (_connection is not { } connection || _isDisposed) { return; }
            try
            {
                using var writer = connection.GetMessageWriter();
                writer.WriteSignalHeader(path: MenuObjectPath, @interface: MenuInterface, member: "LayoutUpdated", signature: "ui");
                writer.WriteUInt32(revision);
                writer.WriteInt32(0);
                if (!connection.TrySendMessage(writer.CreateMessage()))
                {
                    Log.Debug("[LinuxTray] Cannot emit LayoutUpdated: session bus is not connected.");
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[LinuxTray] Failed to emit LayoutUpdated");
            }
        }
    }

    internal void Activate()
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


    internal void WritePixmaps(ref MessageWriter writer)
    {
        var array = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteStructureStart();
        writer.WriteInt32(_iconWidth);
        writer.WriteInt32(_iconHeight);
        writer.WriteArray(_iconPixmap);
        writer.WriteArrayEnd(array);
    }
}
