using CrossMacro.Mcp.DependencyInjection;

namespace CrossMacro.Mcp.Tests;

public sealed class McpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrossMacroMcp_RegistersTheLocalServerAndSecurityPipeline()
    {
        var services = new ServiceCollection();

        var result = services.AddCrossMacroMcp();

        Assert.Same(services, result);
        Assert.Equal(typeof(McpOperationCoordinator), GetImplementationType<IMcpOperationCoordinator>(services));
        Assert.Equal(typeof(McpCommandPolicy), GetImplementationType<IMcpCommandPolicy>(services));
        Assert.Equal(typeof(McpCapabilityPolicy), GetImplementationType<IMcpCapabilityPolicy>(services));
        Assert.Equal(typeof(McpPathPolicy), GetImplementationType<IMcpPathPolicy>(services));
        Assert.Equal(typeof(AutoApprovalService), GetImplementationType<IApprovalService>(services));
        Assert.Equal(typeof(McpAuditStore), GetImplementationType<IMcpAuditStore>(services));
        Assert.Equal(typeof(McpRequestGuard), GetImplementationType<McpRequestGuard>(services));
        Assert.Equal(typeof(McpRuntimeTools), GetImplementationType<McpRuntimeTools>(services));
        Assert.Equal(typeof(McpSettingsTools), GetImplementationType<McpSettingsTools>(services));
        Assert.Equal(typeof(McpProfileTools), GetImplementationType<McpProfileTools>(services));
        Assert.Equal(typeof(McpTextExpansionTools), GetImplementationType<McpTextExpansionTools>(services));
        Assert.Equal(typeof(McpTaskTools), GetImplementationType<McpTaskTools>(services));
        Assert.Equal(typeof(McpAutomationTools), GetImplementationType<McpAutomationTools>(services));
        Assert.Equal(typeof(McpCommandTools), GetImplementationType<McpCommandTools>(services));
        Assert.Equal(typeof(McpMacroTools), GetImplementationType<McpMacroTools>(services));
        Assert.Equal(typeof(McpClipboardTools), GetImplementationType<McpClipboardTools>(services));
        Assert.Equal(typeof(McpWindowTools), GetImplementationType<McpWindowTools>(services));
        Assert.Equal(typeof(McpScreenTools), GetImplementationType<McpScreenTools>(services));
        Assert.Equal(typeof(StdioMcpServer), GetImplementationType<IMcpServer>(services));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void AddCrossMacroMcp_RejectsNullServiceCollection()
    {
        IServiceCollection? services = null;

        _ = Assert.Throws<ArgumentNullException>(() => McpServiceCollectionExtensions.AddCrossMacroMcp(services!));
    }

    private static Type? GetImplementationType<TService>(IServiceCollection services)
    {
        return Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TService)).ImplementationType;
    }
}
