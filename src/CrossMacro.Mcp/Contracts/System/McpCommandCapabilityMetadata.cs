using CrossMacro.Cli;
using CrossMacro.Mcp.Services;
using CrossMacro.Mcp.Services.Operations;
using CrossMacro.Mcp.Services.Security;
namespace CrossMacro.Mcp.Contracts.System;

/// <summary>
/// Security and lifecycle metadata for one CLI command option exposed through MCP.
/// </summary>
public sealed record McpCommandCapabilityMetadata(
    string CommandToken,
    string OptionToken,
    McpCapability Capability,
    McpToolAccess Access,
    McpPathKind? PathKind,
    bool RequiresApproval,
    CliRuntimeProfile RuntimeProfile,
    McpCommandPlatform Platform,
    TimeSpan MaximumDuration);
