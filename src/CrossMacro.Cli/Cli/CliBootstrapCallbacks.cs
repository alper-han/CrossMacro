
namespace CrossMacro.Cli;

public sealed class CliBootstrapCallbacks
{
    public CliBootstrapCallbacks(
        Action<CliParseResult> configureInitialLogging,
        Action<CliCommandOptions> configureCommandLogging,
        Action<CliCommandOptions> configureHostLogging)
    {
        ConfigureInitialLogging = configureInitialLogging ?? throw new ArgumentNullException(nameof(configureInitialLogging));
        ConfigureCommandLogging = configureCommandLogging ?? throw new ArgumentNullException(nameof(configureCommandLogging));
        ConfigureHostLogging = configureHostLogging ?? throw new ArgumentNullException(nameof(configureHostLogging));
    }

    public Action<CliParseResult> ConfigureInitialLogging { get; }
    public Action<CliCommandOptions> ConfigureCommandLogging { get; }
    public Action<CliCommandOptions> ConfigureHostLogging { get; }

    public static CliBootstrapCallbacks NoOp { get; } = new(
        static _ => { },
        static _ => { },
        static _ => { });
}
