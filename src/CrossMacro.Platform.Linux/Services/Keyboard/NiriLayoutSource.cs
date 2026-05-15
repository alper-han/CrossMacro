using System.Text.Json;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.Keyboard;

internal sealed class NiriLayoutSource
{
    private const string KeyboardLayoutsRequestJson = "\"KeyboardLayouts\"";

    private readonly Func<INiriIpcClient> _createIpcClient;
    private readonly Func<string, string?> _resolveLayoutName;

    internal NiriLayoutSource()
        : this(() => new NiriIpcClient(), new XkbLayoutNameResolver().TryResolveLayoutCode)
    {
    }

    internal NiriLayoutSource(Func<INiriIpcClient> createIpcClient, Func<string, string?> resolveLayoutName)
    {
        _createIpcClient = createIpcClient ?? throw new ArgumentNullException(nameof(createIpcClient));
        _resolveLayoutName = resolveLayoutName ?? throw new ArgumentNullException(nameof(resolveLayoutName));
    }

    public string? DetectLayout()
    {
        try
        {
            using var ipcClient = _createIpcClient();
            if (!ipcClient.IsAvailable) return null;

            var response = ipcClient.SendRequestAsync(KeyboardLayoutsRequestJson).GetAwaiter().GetResult();
            return TryParseLayout(response, _resolveLayoutName);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[NiriLayoutSource] Niri IPC failed");
            return null;
        }
    }

    internal static string? TryParseLayout(string? response, Func<string, string?> resolveLayoutName)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return null;

            var keyboardLayouts = root;
            if (root.TryGetProperty("Ok", out var okElement))
            {
                keyboardLayouts = okElement;
            }

            if (keyboardLayouts.ValueKind == JsonValueKind.Object &&
                keyboardLayouts.TryGetProperty("KeyboardLayouts", out var nestedKeyboardLayouts))
            {
                keyboardLayouts = nestedKeyboardLayouts;
            }

            if (keyboardLayouts.ValueKind != JsonValueKind.Object ||
                !keyboardLayouts.TryGetProperty("names", out var names) ||
                names.ValueKind != JsonValueKind.Array ||
                !keyboardLayouts.TryGetProperty("current_idx", out var currentIndex) ||
                !currentIndex.TryGetInt32(out var index) ||
                index < 0 ||
                index >= names.GetArrayLength())
            {
                return null;
            }

            var activeName = names[index].GetString();
            if (string.IsNullOrWhiteSpace(activeName)) return null;

            return resolveLayoutName(activeName);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
