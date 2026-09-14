using CrossMacro.Application.Automation;
using CrossMacro.Application.DependencyInjection;
using CrossMacro.Application.Profiles;
using CrossMacro.Application.Settings;
using CrossMacro.Core.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CrossMacro.Application.Tests.DependencyInjection;

public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void RepeatedRegistration_PreservesExistingPoliciesAndOverrides()
    {
        var services = new ServiceCollection();
        var manager = Substitute.For<IManageSchedule>();
        using var gate = new AutomationTaskMutationGate();
        var authorization = new AutomationTaskAuthorization();
        var settings = new SettingsChangeCoordinator(Substitute.For<ISettingsService>());
        _ = services.AddSingleton(manager);
        _ = services.AddSingleton(gate);
        _ = services.AddSingleton(authorization);
        _ = services.AddSingleton(settings);

        _ = services.AddCrossMacroApplicationServices();
        _ = services.AddCrossMacroApplicationServices();

        Assert.All(services.GroupBy(descriptor => descriptor.ServiceType), group => Assert.Single(group));
        using var provider = services.BuildServiceProvider();
        Assert.Same(manager, provider.GetRequiredService<IManageSchedule>());
        Assert.Same(gate, provider.GetRequiredService<AutomationTaskMutationGate>());
        Assert.Same(authorization, provider.GetRequiredService<AutomationTaskAuthorization>());
        Assert.Same(settings, provider.GetRequiredService<SettingsChangeCoordinator>());
        Assert.Same(provider.GetRequiredService<IScheduleCommands>(), provider.GetRequiredService<IScheduleCommands>());
        var profileOperations = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IProfileOperations));
        Assert.Equal(typeof(ProfileOperations), profileOperations.ImplementationType);
    }

}
