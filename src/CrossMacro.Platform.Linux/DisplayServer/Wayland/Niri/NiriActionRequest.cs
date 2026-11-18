using System.Buffers;
using System.Text.Json;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

/// <summary>
/// Builds Niri <c>Action</c> requests with a JSON writer so caller-supplied text is always escaped.
/// </summary>
internal static class NiriActionRequest
{
    public static string FocusWindow(ulong id) =>
        Build("FocusWindow", w => w.WriteNumber("id", id));

    public static string CloseWindow(ulong id) =>
        Build("CloseWindow", w => w.WriteNumber("id", id));

    public static string FullscreenActiveWindow() => BuildForActiveWindow("FullscreenWindow");

    public static string MaximizeActiveWindowToEdges() => BuildForActiveWindow("MaximizeWindowToEdges");

    public static string MoveActiveWindowToFloating() => BuildForActiveWindow("MoveWindowToFloating");

    public static string MoveActiveWindowToTiling() => BuildForActiveWindow("MoveWindowToTiling");

    public static string SetActiveWindowWidth(int width) =>
        Build("SetWindowWidth", w => WriteActiveWindowChange(w, width));

    public static string SetActiveWindowHeight(int height) =>
        Build("SetWindowHeight", w => WriteActiveWindowChange(w, height));

    public static string MoveActiveFloatingWindow(int x, int y) =>
        Build("MoveFloatingWindow", w =>
        {
            w.WriteNull("id");
            WriteFixed(w, "x", x);
            WriteFixed(w, "y", y);
        });

    /// <summary>Focuses a workspace; a purely numeric reference is an id, anything else a name.</summary>
    public static string FocusWorkspace(string workspace) =>
        Build("FocusWorkspace", w => WriteWorkspaceReference(w, workspace));

    /// <summary>Moves the active window (<paramref name="windowId"/> null) or the given window to a workspace.</summary>
    public static string MoveWindowToWorkspace(ulong? windowId, string workspace) =>
        Build("MoveWindowToWorkspace", w => WriteMoveWindowToWorkspace(w, windowId, workspace));

    public static string MoveWindowToWorkspace(ulong? windowId, ulong workspaceId) =>
        MoveWindowToWorkspace(windowId, workspaceId.ToString(CultureInfo.InvariantCulture));

    private static string BuildForActiveWindow(string action) =>
        Build(action, static w => w.WriteNull("id"));

    private static void WriteActiveWindowChange(Utf8JsonWriter writer, int size)
    {
        writer.WriteNull("id");
        writer.WriteStartObject("change");
        writer.WriteNumber("SetFixed", size);
        writer.WriteEndObject();
    }

    private static void WriteFixed(Utf8JsonWriter writer, string axis, int value)
    {
        writer.WriteStartObject(axis);
        writer.WriteNumber("SetFixed", (double)value);
        writer.WriteEndObject();
    }

    private static void WriteMoveWindowToWorkspace(Utf8JsonWriter writer, ulong? windowId, string workspace)
    {
        if (windowId is { } id)
        {
            writer.WriteNumber("id", id);
        }
        else
        {
            writer.WriteNull("id");
        }

        WriteWorkspaceReference(writer, workspace);
        writer.WriteBoolean("focus", value: false);
    }

    private static void WriteWorkspaceReference(Utf8JsonWriter writer, string workspace)
    {
        writer.WriteStartObject("reference");
        if (ulong.TryParse(workspace, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            writer.WriteNumber("Id", id);
        }
        else
        {
            writer.WriteString("Name", workspace);
        }

        writer.WriteEndObject();
    }

    private static string Build(string action, Action<Utf8JsonWriter> writeFields)
    {
        var buffer = new ArrayBufferWriter<byte>(128);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("Action");
            writer.WriteStartObject(action);
            writeFields(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
