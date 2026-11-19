namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
    public ShellCommandMode ShellCommandMode
    {
        get => _shell.ShellCommandMode;
        set => SetScriptField(ref _shell.ShellCommandMode, value);
    }

    public string ShellCommand
    {
        get => _shell.ShellCommand;
        set => SetScriptField(ref _shell.ShellCommand, value ?? string.Empty);
    }

    public string ShellStandardInput
    {
        get => _shell.ShellStandardInput;
        set => SetScriptField(ref _shell.ShellStandardInput, value ?? string.Empty);
    }

    public string ShellExitCodeVariableName
    {
        get => _shell.ShellExitCodeVariableName;
        set => SetScriptField(ref _shell.ShellExitCodeVariableName, value?.Trim() ?? string.Empty);
    }

    public string ShellStandardOutputVariableName
    {
        get => _shell.ShellStandardOutputVariableName;
        set => SetScriptField(ref _shell.ShellStandardOutputVariableName, value?.Trim() ?? string.Empty);
    }

    public string ShellStandardErrorVariableName
    {
        get => _shell.ShellStandardErrorVariableName;
        set => SetScriptField(ref _shell.ShellStandardErrorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ShellRetries
    {
        get => _shell.ShellRetries;
        set => SetScriptField(ref _shell.ShellRetries, value);
    }

    public int ShellBackoffMs
    {
        get => _shell.ShellBackoffMs;
        set => SetScriptField(ref _shell.ShellBackoffMs, value);
    }

    public int ShellTimeoutMs
    {
        get => _shell.ShellTimeoutMs;
        set => SetScriptField(ref _shell.ShellTimeoutMs, value);
    }

    public bool TryGetShellPayload(out EditorActionShellPayload payload) =>
        EditorActionShellPayload.TryCreate(this, out payload);
}
