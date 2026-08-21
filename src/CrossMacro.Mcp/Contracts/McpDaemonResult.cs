namespace CrossMacro.Mcp.Contracts;

public sealed record McpDaemonResult(
    string Action,
    McpToolOutcome Outcome,
    string? SocketPath,
    string HandshakeStatus,
    string SocketAccessStatus,
    string? Message,
    bool LinuxOnly);
