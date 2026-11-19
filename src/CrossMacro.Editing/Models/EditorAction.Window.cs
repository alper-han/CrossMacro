using CrossMacro.Core.Services.Scripting;

namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
    public WindowCommandMode WindowCommandMode
    {
        get => _window.WindowCommandMode;
        set => SetScriptField(ref _window.WindowCommandMode, value);
    }

    public string WindowSelectorKind
    {
        get => _window.WindowSelectorKind;
        set => SetScriptField(
            ref _window.WindowSelectorKind,
            value?.Trim().ToUpperInvariant() switch
            {
                "ACTIVE" => "active",
                "TITLE" => "title",
                "CLASS" => "class",
                "ADDRESS" => "address",
                _ => value?.Trim() ?? string.Empty,
            });
    }

    public string WindowSelectorValue
    {
        get => _window.WindowSelectorValue;
        set => SetScriptField(ref _window.WindowSelectorValue, value ?? string.Empty);
    }

    public string WindowActiveField
    {
        get => _window.WindowActiveField;
        set => SetScriptField(
            ref _window.WindowActiveField,
            WindowActiveFieldSyntax.Normalize(value));
    }

    public string WindowOutputVariable
    {
        get => _window.WindowOutputVariable;
        set => SetScriptField(ref _window.WindowOutputVariable, value?.Trim() ?? string.Empty);
    }

    public int WindowTimeoutMs
    {
        get => _window.WindowTimeoutMs;
        set => SetScriptField(ref _window.WindowTimeoutMs, value);
    }

    public int WindowX
    {
        get => _window.WindowX;
        set => SetScriptField(ref _window.WindowX, value);
    }

    public int WindowY
    {
        get => _window.WindowY;
        set => SetScriptField(ref _window.WindowY, value);
    }

    public int WindowWidth
    {
        get => _window.WindowWidth;
        set => SetScriptField(ref _window.WindowWidth, value);
    }

    public int WindowHeight
    {
        get => _window.WindowHeight;
        set => SetScriptField(ref _window.WindowHeight, value);
    }

    public string WindowWorkspace
    {
        get => _window.WindowWorkspace;
        set => SetScriptField(ref _window.WindowWorkspace, value ?? string.Empty);
    }

    public bool TryGetWindowPayload(out EditorActionWindowPayload payload) =>
        EditorActionWindowPayload.TryCreate(this, out payload);
}
