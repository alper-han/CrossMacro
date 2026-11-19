using CrossMacro.Core.Models.Settings;
using CrossMacro.Core.Services.Settings;
using NSubstitute;
using System;
using System.Threading.Tasks;

namespace CrossMacro.TestInfrastructure;

/// <summary>
/// A settings substitute whose synchronous state callbacks execute against its configured Current value.
/// <see cref="ISettingsService.AccessCurrent{T}"/> is generic and NSubstitute cannot intercept unknown instantiations, so only the
/// result types the production callers use are wired: <see cref="AppSettings"/>, <see cref="Task"/>, <see cref="bool"/> and <see cref="string"/>.
/// Any other result type returns <c>default</c>; add it to <see cref="Create"/> when a new caller needs it.
/// </summary>
public static class SettingsServiceSubstitute
{
    public static ISettingsService Create()
    {
        var service = Substitute.For<ISettingsService>();
        ConfigureAccess<AppSettings>(service);
        ConfigureAccess<Task>(service);
        ConfigureAccess<bool>(service);
        ConfigureAccess<string>(service);
        return service;
    }

    private static void ConfigureAccess<T>(ISettingsService service) =>
        service.AccessCurrent(Arg.Any<Func<AppSettings, T>>()).Returns(call => call.Arg<Func<AppSettings, T>>()(service.Current));
}
