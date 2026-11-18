
namespace CrossMacro.Infrastructure.DependencyInjection;

/// <summary>
/// Shared runtime DI registrations consumed by both GUI and CLI hosts.
/// The two public entry points intentionally separate registrations that are safe
/// before platform wiring from shared runtime registrations that depend on
/// platform-provided seams being available. Each feature registers only its own
/// types in its own file. <c>TryAdd</c> is used only for contracts a platform or host
/// may already have supplied (paths, clock, key codes, screen providers); everything
/// else is added unconditionally.
/// </summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared runtime services that do not require platform-specific
    /// implementations to be present yet.
    /// </summary>
    public static IServiceCollection AddCrossMacroCommonRuntimeServices(this IServiceCollection services)
    {
        CommonRuntimeServiceRegistration.Register(services);

        return services;
    }

    /// <summary>
    /// Registers shared runtime services that are composed after platform-specific
    /// services have supplied the required input, display, and simulation seams.
    /// </summary>
    public static IServiceCollection AddCrossMacroSharedPostPlatformRuntimeServices(
        this IServiceCollection services,
        Func<IServiceProvider, IInputSimulatorPool?> simulatorPoolResolver)
    {
        ArgumentNullException.ThrowIfNull(simulatorPoolResolver);

        PostPlatformRuntimeServiceRegistration.Register(services);
        HotkeyRuntimeServiceRegistration.Register(services);
        ScreenReadingRuntimeServiceRegistration.Register(services);
        RecordingRuntimeServiceRegistration.Register(services);
        PlaybackRuntimeServiceRegistration.Register(services, simulatorPoolResolver);
        WorkflowRuntimeServiceRegistration.Register(services);
        TextExpansionRuntimeServiceRegistration.Register(services);
        ProfileRuntimeServiceRegistration.Register(services);

        return services;
    }

}
