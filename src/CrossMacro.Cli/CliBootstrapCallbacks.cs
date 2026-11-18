
namespace CrossMacro.Cli;

public sealed class CliBootstrapCallbacks(
    Action<CliParseResult> configureInitialLogging,
    Action<CliCommandOptions> configureCommandLogging)
{
    public Action<CliParseResult> ConfigureInitialLogging { get; } = configureInitialLogging ?? throw new ArgumentNullException(nameof(configureInitialLogging));
    public Action<CliCommandOptions> ConfigureCommandLogging { get; } = configureCommandLogging ?? throw new ArgumentNullException(nameof(configureCommandLogging));

    public static CliBootstrapCallbacks NoOp { get; } = new(
        static _ => { },
        static _ => { });
}
