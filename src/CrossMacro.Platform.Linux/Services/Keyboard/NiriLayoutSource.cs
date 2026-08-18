
namespace CrossMacro.Platform.Linux.Services.Keyboard;

internal sealed class NiriLayoutSource
{
    private const string KeyboardLayoutsRequestJson = "\"KeyboardLayouts\"";

    private readonly Func<INiriIpcClient> _createIpcClient;
    private readonly Func<string, string?> _resolveLayoutName;

    internal NiriLayoutSource()
        : this(static () => new NiriIpcClient(), new XkbLayoutNameResolver().TryResolveLayoutCode) { /* Empty */ }

    internal NiriLayoutSource(Func<INiriIpcClient> createIpcClient, Func<string, string?> resolveLayoutName)
    {
        _createIpcClient = createIpcClient ?? throw new ArgumentNullException(nameof(createIpcClient));
        _resolveLayoutName = resolveLayoutName ?? throw new ArgumentNullException(nameof(resolveLayoutName));
    }

    public async Task<string?> DetectLayoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var ipcClient = _createIpcClient();
            if (!ipcClient.IsAvailable)
            {
                return null;
            }

            var response = await ipcClient.SendRequestAsync(KeyboardLayoutsRequestJson, cancellationToken).ConfigureAwait(false);
            return TryParseLayout(response, _resolveLayoutName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[NiriLayoutSource] Niri IPC failed");
            return null;
        }
    }

    internal static string? TryParseLayout(string? response, Func<string, string?> resolveLayoutName)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            var keyboardLayouts = root;
            if (root.TryGetProperty("Ok", out var okElement))
            {
                keyboardLayouts = okElement;
            }

            if (keyboardLayouts.ValueKind is JsonValueKind.Object && keyboardLayouts.TryGetProperty("KeyboardLayouts", out var nestedKeyboardLayouts))
            {
                keyboardLayouts = nestedKeyboardLayouts;
            }

            if (keyboardLayouts.ValueKind is not JsonValueKind.Object ||
                !keyboardLayouts.TryGetProperty("names", out var names) ||
                names.ValueKind is not JsonValueKind.Array ||
                !keyboardLayouts.TryGetProperty("current_idx", out var currentIndex) ||
                !currentIndex.TryGetInt32(out var index) ||
                index < 0 ||
                index >= names.GetArrayLength())
            {
                return null;
            }

            var activeName = names[index].GetString();
            if (string.IsNullOrWhiteSpace(activeName))
            {
                return null;
            }

            return resolveLayoutName(activeName);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
