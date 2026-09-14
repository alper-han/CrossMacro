using CrossMacro.Core.Models.Settings;
using CrossMacro.Core.Services.Settings;
using NSubstitute;
using System;
using System.Threading.Tasks;

namespace CrossMacro.Tests;

/// <summary>A settings substitute whose synchronous state callbacks execute against its configured Current value.</summary>
internal static class SettingsServiceSubstitute
{
    internal static ISettingsService Create()
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
