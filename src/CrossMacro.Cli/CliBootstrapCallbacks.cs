
namespace CrossMacro.Cli;

public sealed class CliBootstrapCallbacks(
    Action<CliParseResult> configureInitialLogging,
    Action<CommandOptions> configureCommandLogging)
{
    public Action<CliParseResult> ConfigureInitialLogging { get; } = configureInitialLogging ?? throw new ArgumentNullException(nameof(configureInitialLogging));
    public Action<CommandOptions> ConfigureCommandLogging { get; } = configureCommandLogging ?? throw new ArgumentNullException(nameof(configureCommandLogging));

    public static CliBootstrapCallbacks NoOp { get; } = new(
        static _ => { },
        static _ => { });
}
