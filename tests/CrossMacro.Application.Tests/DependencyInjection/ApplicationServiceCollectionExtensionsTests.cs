using CrossMacro.Application.DependencyInjection;
using CrossMacro.Application.Profiles;
using CrossMacro.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Application.Tests.DependencyInjection;

public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void RepeatedRegistration_PreservesExistingPoliciesAndOverrides()
    {
        var services = new ServiceCollection();
        var manager = Substitute.For<IManageRunnableAutomationTasks<ScheduledTask>>();
        using var gate = new AutomationTaskMutationGate();
        var authorization = new AutomationTaskAuthorization();
        var settings = new SettingsChangeCoordinator(SettingsServiceSubstitute.Create());
        _ = services.AddSingleton(manager);
        _ = services.AddSingleton(gate);
        _ = services.AddSingleton(authorization);
        _ = services.AddSingleton(settings);

        _ = services.AddCrossMacroApplicationServices();
        _ = services.AddCrossMacroApplicationServices();

        Assert.All(services.GroupBy(descriptor => descriptor.ServiceType), group => Assert.Single(group));
        using var provider = services.BuildServiceProvider();
        Assert.Same(manager, provider.GetRequiredService<IManageRunnableAutomationTasks<ScheduledTask>>());
        Assert.Same(gate, provider.GetRequiredService<AutomationTaskMutationGate>());
        Assert.Same(authorization, provider.GetRequiredService<AutomationTaskAuthorization>());
        Assert.Same(settings, provider.GetRequiredService<SettingsChangeCoordinator>());
        Assert.Same(provider.GetRequiredService<IRunnableTaskCommands<ScheduledTask, ScheduleCommand>>(), provider.GetRequiredService<IRunnableTaskCommands<ScheduledTask, ScheduleCommand>>());
        var profileOperations = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IProfileOperations));
        Assert.Equal(typeof(ProfileOperations), profileOperations.ImplementationType);
    }

}
