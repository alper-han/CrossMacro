using CrossMacro.Commands.Runtime;
using CrossMacro.Commands.Settings;
using CrossMacro.Mcp.DependencyInjection;
using CrossMacro.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Mcp.Tests;

public sealed class McpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrossMacroMcp_RegistersTheLocalServerAndSecurityPipeline()
    {
        var services = new ServiceCollection();

        var result = services.AddCrossMacroMcp();

        Assert.Same(services, result);
        Assert.Equal(typeof(McpOperationCoordinator), GetImplementationType<McpOperationCoordinator>(services));
        Assert.Equal(typeof(McpCapabilityPolicy), GetImplementationType<IMcpCapabilityPolicy>(services));
        Assert.Equal(typeof(McpPathPolicy), GetImplementationType<IMcpPathPolicy>(services));
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
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IClipboardCommandService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISettingsCommandService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPreflightService));
    }

    [Fact]
    public void AddCrossMacroMcp_RejectsNullServiceCollection()
    {
        IServiceCollection? services = null;

        _ = Assert.Throws<ArgumentNullException>(() => McpServiceCollectionExtensions.AddCrossMacroMcp(services!));
    }

    [Fact]
    public void AddCrossMacroMcp_PreservesExistingTryAddDependencies()
    {
        var services = new ServiceCollection();
        var timeProvider = TimeProvider.System;
        _ = services.AddSingleton(timeProvider);

        _ = services.AddCrossMacroMcp();

        var timeDescriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
        Assert.Same(timeProvider, timeDescriptor.ImplementationInstance);
    }

    private static Type? GetImplementationType<TService>(IServiceCollection services)
    {
        return Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TService)).ImplementationType;
    }
}
