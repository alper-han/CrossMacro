using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Cli;

public sealed class CliCommandLine(CliCommandExecutor executor) : ICommandLine
{
    private readonly CliCommandExecutor _executor = executor;

    public bool TryParse(ReadOnlyMemory<string> arguments, [NotNullWhen(true)] out CommandOptions? options, out string? errorMessage)
    {
        var result = CliCommandRouter.Parse(arguments);
        options = result.Kind is CliParseResult.ParseResultKind.Success ? result.Options : null;
        errorMessage = result.ErrorMessage;
        return options is not null;
    }

    public Task<OperationResult> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken) =>
        _executor.ExecuteResultAsync(options, cancellationToken);
}
