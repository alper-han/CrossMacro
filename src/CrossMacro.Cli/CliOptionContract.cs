namespace CrossMacro.Cli;

/// <summary>
/// Stable metadata for one CLI option token.
/// </summary>
public sealed record CliOptionContract
{
    public CliOptionContract(
        string token,
        CliOptionValueKind valueKind,
        bool requiresValue,
        string? defaultValue = null,
        IReadOnlyList<string>? allowedValues = null)
    {
        Token = token;
        ValueKind = valueKind;
        RequiresValue = requiresValue;
        DefaultValue = defaultValue;
        AllowedValues = Array.AsReadOnly((allowedValues ?? []).ToArray());
    }

    public string Token { get; }

    public CliOptionValueKind ValueKind { get; }

    public bool RequiresValue { get; }

    public string? DefaultValue { get; }

    public IReadOnlyList<string> AllowedValues { get; }
}
