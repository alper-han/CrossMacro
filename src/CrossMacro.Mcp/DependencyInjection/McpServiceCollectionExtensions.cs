using CrossMacro.Commands.DependencyInjection;
namespace CrossMacro.Mcp.DependencyInjection;

public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the local stdio MCP host after the executable root has added
    /// its platform and common runtime services. The host that owns the command
    /// grammar registers <see cref="ICommandLine"/>, which <c>command.execute</c> uses.
    /// </summary>
    public static IServiceCollection AddCrossMacroMcp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        _ = services.AddCrossMacroCommandServices();
        _ = services.AddSingleton<IMcpOperationCoordinator, McpOperationCoordinator>();
        _ = services.AddSingleton<McpAutomationExecution>();
        _ = services.AddSingleton<IMcpCommandPolicy, McpCommandPolicy>();
        _ = services.AddSingleton<IMcpCapabilityPolicy, McpCapabilityPolicy>();
        _ = services.AddSingleton<IMcpPathPolicy, McpPathPolicy>();
        _ = services.AddSingleton<McpPathAuthorizer>();
        _ = services.AddSingleton<McpToolAuthorization>();
        _ = services.AddSingleton<McpRuntimeTools>();
        _ = services.AddSingleton<McpSettingsTools>();
        _ = services.AddSingleton<McpProfileTools>();
        _ = services.AddSingleton<McpTextExpansionTools>();
        _ = services.AddSingleton<McpTaskTools>();
        _ = services.AddSingleton<McpAutomationTools>();
        _ = services.AddSingleton<McpCommandTools>();
        _ = services.AddSingleton<McpMacroTools>();
        _ = services.AddSingleton<McpClipboardTools>();
        _ = services.AddSingleton<McpWindowTools>();
        _ = services.AddSingleton<McpScreenTools>();
        _ = services.AddSingleton<McpRequestGuard>();
        _ = services.AddSingleton<IMcpServer, StdioMcpServer>();
        return services;
    }
}
