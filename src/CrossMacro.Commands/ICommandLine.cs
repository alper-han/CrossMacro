namespace CrossMacro.Commands;

/// <summary>
/// Parses and executes command lines in the CLI grammar. The CLI host owns the grammar and the
/// handlers behind this port, so a host that accepts command lines, such as the MCP server,
/// depends on the port rather than on the CLI.
/// </summary>
public interface ICommandLine
{
    /// <summary>Parses one command followed by its arguments; <paramref name="errorMessage"/> may be null when parsing fails.</summary>
    public bool TryParse(ReadOnlyMemory<string> arguments, [NotNullWhen(true)] out CommandOptions? options, out string? errorMessage);

    public Task<OperationResult> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken);
}
