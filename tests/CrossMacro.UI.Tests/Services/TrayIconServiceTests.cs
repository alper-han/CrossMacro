namespace CrossMacro.UI.Tests.Services;

using System;
using CrossMacro.UI.Services;

public class TrayIconServiceTests
{
    [Fact]
    public void IsTraySupported_WhenFlatpakIdPresent_ReturnsFalse()
    {
        using var _ = new EnvironmentVariableScope("FLATPAK_ID", "io.github.test.crossmacro");

        Assert.False(TrayIconService.IsTraySupported());
    }

    [Fact]
    public void IsTraySupported_WhenCrossMacroFlatpakFlagSet_ReturnsFalse()
    {
        using var _ = new EnvironmentVariableScope("CROSSMACRO_FLATPAK", "1");

        Assert.False(TrayIconService.IsTraySupported());
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
