using CrossMacro.Core.Services.Recording;
using CrossMacro.Infrastructure.Services.Recording;
using CrossMacro.Infrastructure.Services.Recording.Processors;
using CrossMacro.Platform.Abstractions.Recording.Strategies;

namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class RecordingRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<Func<ICoordinateStrategy, IInputEventProcessor>>(_ => strategy => new StandardInputEventProcessor(strategy));
        _ = services.AddTransient<IMacroRecorder>(sp => new MacroRecorder(
            sp.GetService<Func<IInputCapture>>(), sp.GetRequiredService<ICoordinateStrategyFactory>(),
            sp.GetRequiredService<Func<ICoordinateStrategy, IInputEventProcessor>>(), sp.GetService<Func<IInputSimulator>>(),
            sp.GetService<IMousePositionProvider>(), sp.GetService<IInputSimulatorPool>()));
    }
}
