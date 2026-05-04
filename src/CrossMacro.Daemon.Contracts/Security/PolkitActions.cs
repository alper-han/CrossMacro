namespace CrossMacro.Daemon.Contracts.Security;

public static class PolkitActions
{
    public const string InputCapture = "io.github.alper_han.crossmacro.input-capture";
    public const string InputSimulate = "io.github.alper_han.crossmacro.input-simulate";

    public static readonly string[] All =
    [
        InputCapture,
        InputSimulate
    ];
}
