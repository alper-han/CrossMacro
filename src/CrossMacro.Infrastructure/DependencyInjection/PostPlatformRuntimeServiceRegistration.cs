using CrossMacro.Infrastructure.Services.Macros;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class PostPlatformRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<IKeyCodeMapper, KeyCodeMapper>();
        _ = services.AddSingleton<Func<IKeyCodeMapper>>(sp => sp.GetRequiredService<IKeyCodeMapper>);
        _ = services.AddSingleton<IMacroFileManager, MacroFileManager>();
        _ = services.AddSingleton<IScriptValidationService, ScriptValidationService>();
        _ = services.AddSingleton<IRunExecutionService>(sp => new RunScriptRuntimeService(sp.GetRequiredService<Func<IMacroPlayer>>(), sp.GetRequiredService<IKeyCodeMapper>(), sp.GetService<IMousePositionProvider>()));
    }
}
