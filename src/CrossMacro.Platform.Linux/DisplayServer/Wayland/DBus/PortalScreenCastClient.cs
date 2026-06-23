using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal delegate void PortalMessageWriterAction(ref MessageWriter writer);

internal interface IPortalScreenCastSessionClient : IDisposable
{
    Task<PortalScreenCastSession> StartAsync(ScreenReadOptions options, string? restoreToken = null);

    void DisposeIfNotOwnedBySession();
}

internal interface IPortalScreenCastSessionClientFactory
{
    Task<IPortalScreenCastSessionClient> ConnectAsync();
}

internal sealed class PortalScreenCastSessionClientFactory : IPortalScreenCastSessionClientFactory
{
    public static PortalScreenCastSessionClientFactory Instance { get; } = new();

    public async Task<IPortalScreenCastSessionClient> ConnectAsync() => await PortalScreenCastClient.ConnectAsync().ConfigureAwait(false);
}

internal sealed class PortalScreenCastClient : IPortalScreenCastSessionClient
{
    private const string Service = "org.freedesktop.portal.Desktop";
    private const string DesktopPath = "/org/freedesktop/portal/desktop";
    private const string ScreenCastInterface = "org.freedesktop.portal.ScreenCast";
    private const string RequestInterface = "org.freedesktop.portal.Request";

    private readonly DBusConnection _connection;
    private bool _ownedBySession;

    private PortalScreenCastClient(DBusConnection connection)
    {
        _connection = connection;
    }

    public static async Task<PortalScreenCastClient> ConnectAsync()
    {
        var connection = LinuxDbusTransportBoundary.CreateSessionConnection();
        await connection.ConnectAsync().ConfigureAwait(false);
        return new PortalScreenCastClient(connection);
    }

    public async Task<PortalScreenCastSession> StartAsync(ScreenReadOptions options, string? restoreToken = null)
    {
        options.CancellationToken.ThrowIfCancellationRequested();

        var token = Guid.NewGuid().ToString("N");
        var sessionHandleToken = $"crossmacro_session_{token}";
        var createHandleToken = $"crossmacro_create_{token}";
        var createResponse = await CallRequestAsync("CreateSession", "a{sv}", createHandleToken, options, (ref MessageWriter writer) =>
        {
            writer.WriteDictionary(new Dictionary<string, VariantValue>
            {
                ["session_handle_token"] = VariantValue.String(sessionHandleToken),
                ["handle_token"] = VariantValue.String(createHandleToken)
            });
        }).ConfigureAwait(false);

        var sessionHandle = GetResponseObjectPath(createResponse.Results, "session_handle");

        var selectHandleToken = $"crossmacro_select_{token}";
        var selectOptions = BuildSelectSourcesOptions(selectHandleToken, restoreToken);

        await CallRequestAsync("SelectSources", "oa{sv}", selectHandleToken, options, (ref MessageWriter writer) =>
        {
            writer.WriteObjectPath(sessionHandle);
            writer.WriteDictionary(selectOptions);
        }).ConfigureAwait(false);

        var startHandleToken = $"crossmacro_start_{token}";
        var startResponse = await CallRequestAsync("Start", "osa{sv}", startHandleToken, options, (ref MessageWriter writer) =>
        {
            writer.WriteObjectPath(sessionHandle);
            writer.WriteString(string.Empty);
            writer.WriteDictionary(new Dictionary<string, VariantValue>
            {
                ["handle_token"] = VariantValue.String(startHandleToken)
            });
        }).ConfigureAwait(false);

        var streams = ParseStreams(startResponse.Results);
        var refreshedRestoreToken = TryGetResponseString(startResponse.Results, "restore_token");
        var remote = await OpenPipeWireRemoteAsync(sessionHandle).WaitAsync(GetTimeout(options), options.CancellationToken).ConfigureAwait(false);
        _ownedBySession = true;
        return new PortalScreenCastSession(sessionHandle, streams, remote, this, refreshedRestoreToken);
    }

    public void Dispose() => _connection.Dispose();

    public void DisposeIfNotOwnedBySession()
    {
        if (!_ownedBySession)
        {
            Dispose();
        }
    }

    private async Task<PortalResponse> CallRequestAsync(
        string member,
        string signature,
        string handleToken,
        ScreenReadOptions options,
        PortalMessageWriterAction writeBody)
    {
        var requestPath = GetRequestPath(handleToken);
        var completion = new TaskCompletionSource<PortalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = await WatchResponseAsync(requestPath, completion).ConfigureAwait(false);

        var writer = _connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, DesktopPath, ScreenCastInterface, member, signature);
        writeBody(ref writer);
        var returnedRequestPath = await _connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return reader.ReadObjectPathAsString();
        }).WaitAsync(GetTimeout(options), options.CancellationToken).ConfigureAwait(false);

        if (!StringComparer.Ordinal.Equals(requestPath, returnedRequestPath))
        {
            throw new PortalScreenCastException(
                ScreenReadErrorKind.CaptureFailed,
                $"Portal returned unexpected request path '{returnedRequestPath}', expected '{requestPath}'.");
        }

        return await completion.Task.WaitAsync(GetTimeout(options), options.CancellationToken).ConfigureAwait(false);
    }

    private async Task<IDisposable> WatchResponseAsync(string requestPath, TaskCompletionSource<PortalResponse> completion)
    {
        return await _connection.WatchSignalAsync<PortalResponse>(
            Service,
            requestPath,
            RequestInterface,
            "Response",
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                var response = reader.ReadUInt32();
                var results = reader.ReadDictionaryOfStringToVariantValue();
                return new PortalResponse(response, results);
            },
            (exception, response) =>
            {
                if (exception is not null)
                {
                    completion.TrySetException(exception);
                }
                else if (response.ResponseCode == 0)
                {
                    completion.TrySetResult(response);
                }
                else
                {
                    completion.TrySetException(new PortalScreenCastException(
                        MapResponseCode(response.ResponseCode),
                        $"XDG Desktop Portal ScreenCast request failed with response={response.ResponseCode}."));
                }
            },
            readerState: null,
            emitOnCapturedContext: false,
            flags: ObserverFlags.None).ConfigureAwait(false);
    }

    private string GetRequestPath(string handleToken)
    {
        var uniqueName = _connection.UniqueName ?? throw new PortalScreenCastException(ScreenReadErrorKind.BackendUnavailable, "D-Bus connection does not have a unique name.");
        var sender = uniqueName.TrimStart(':').Replace('.', '_');
        return $"/org/freedesktop/portal/desktop/request/{sender}/{handleToken}";
    }

    private async Task<SafeFileHandle> OpenPipeWireRemoteAsync(string sessionHandle)
    {
        var writer = _connection.GetMessageWriter();
        writer.WriteMethodCallHeader(Service, DesktopPath, ScreenCastInterface, "OpenPipeWireRemote", "oa{sv}");
        writer.WriteObjectPath(sessionHandle);
        writer.WriteDictionary(Array.Empty<KeyValuePair<string, VariantValue>>());
        return await _connection.CallMethodAsync(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return reader.ReadHandle<SafeFileHandle>();
        }).ConfigureAwait(false);
    }

    private static string GetResponseObjectPath(IReadOnlyDictionary<string, VariantValue> results, string key)
    {
        if (!results.TryGetValue(key, out var value))
        {
            throw new PortalScreenCastException(ScreenReadErrorKind.CaptureFailed, $"Portal response did not include '{key}'.");
        }

        return value.Type == VariantValueType.ObjectPath ? value.GetObjectPathAsString() : value.GetString();
    }

    internal static Dictionary<string, VariantValue> BuildSelectSourcesOptions(string handleToken, string? restoreToken)
    {
        var options = new Dictionary<string, VariantValue>
        {
            ["types"] = VariantValue.UInt32(1),
            ["multiple"] = VariantValue.Bool(true),
            ["cursor_mode"] = VariantValue.UInt32(1),
            ["persist_mode"] = VariantValue.UInt32(2),
            ["handle_token"] = VariantValue.String(handleToken)
        };

        if (!string.IsNullOrWhiteSpace(restoreToken))
        {
            options["restore_token"] = VariantValue.String(restoreToken);
        }

        return options;
    }

    internal static string? TryGetResponseString(IReadOnlyDictionary<string, VariantValue> results, string key)
    {
        if (!results.TryGetValue(key, out var value))
        {
            return null;
        }

        var result = value.Type == VariantValueType.ObjectPath ? value.GetObjectPathAsString() : value.GetString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    internal static IReadOnlyList<PortalStream> ParseStreams(IReadOnlyDictionary<string, VariantValue> results)
    {
        if (!results.TryGetValue("streams", out var streamsValue))
        {
            throw new PortalScreenCastException(ScreenReadErrorKind.CaptureFailed, "Portal Start response did not include streams.");
        }

        var streams = new List<PortalStream>();
        for (var i = 0; i < streamsValue.Count; i++)
        {
            var stream = streamsValue.GetItem(i);
            var nodeId = stream.GetItem(0).GetUInt32();
            var properties = UnboxDictionary(stream.GetItem(1));
            streams.Add(new PortalStream(nodeId, properties));
        }

        if (streams.Count == 0)
        {
            throw new PortalScreenCastException(ScreenReadErrorKind.CaptureFailed, "Portal Start response did not include any streams.");
        }

        return streams;
    }

    private static IReadOnlyDictionary<string, object> UnboxDictionary(VariantValue value)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < value.Count; i++)
        {
            var entry = value.GetDictionaryEntry(i);
            result[entry.Key.GetString()] = UnboxVariant(entry.Value);
        }

        return result;
    }

    private static object UnboxVariant(VariantValue value) => value.Type switch
    {
        VariantValueType.Bool => value.GetBool(),
        VariantValueType.UInt32 => value.GetUInt32(),
        VariantValueType.Int32 => value.GetInt32(),
        VariantValueType.UInt64 => value.GetUInt64(),
        VariantValueType.String => value.GetString(),
        VariantValueType.ObjectPath => value.GetObjectPathAsString(),
        VariantValueType.Array => Enumerable.Range(0, value.Count).Select(i => UnboxVariant(value.GetItem(i))).ToArray(),
        VariantValueType.Struct => Enumerable.Range(0, value.Count).Select(i => UnboxVariant(value.GetItem(i))).ToArray(),
        VariantValueType.Dictionary => UnboxDictionary(value),
        VariantValueType.Variant => UnboxVariant(value.GetVariantValue()),
        _ => value.ToString() ?? string.Empty
    };

    private static ScreenReadErrorKind MapResponseCode(uint responseCode) => responseCode switch
    {
        1 => ScreenReadErrorKind.PermissionDenied,
        _ => ScreenReadErrorKind.CaptureFailed
    };

    private static TimeSpan GetTimeout(ScreenReadOptions options) => options.Timeout ?? TimeSpan.FromMinutes(2);

    private readonly record struct PortalResponse(uint ResponseCode, Dictionary<string, VariantValue> Results);
}
