using System;
using Avalonia.Controls;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.Startup;
using NSubstitute;

namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopStartupRuntimeServiceTests
{
    [Fact]
    public void CreateDisplayPlan_WhenDisplayModeIsVisible_UsesNormalWindowWithLastWindowClose()
    {
        var service = CreateService();
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: false,
            PersistTrayEnabled: false,
            UseStartupTrayOnly: false);

        var plan = service.CreateDisplayPlan(preferences, trayAvailable: true);

        Assert.Equal(DesktopStartupDisplayMode.Visible, plan.DisplayMode);
        Assert.Equal(ShutdownMode.OnLastWindowClose, plan.ShutdownMode);
        Assert.True(plan.ShowInTaskbar);
        Assert.True(plan.ShowActivated);
        Assert.Equal(WindowState.Normal, plan.WindowState);
        Assert.False(plan.DisableStartupOnlyTrayAfterInitialRestore);
    }

    [Fact]
    public void CreateDisplayPlan_WhenTrayUnavailable_StartsMinimizedWindow()
    {
        var service = CreateService();
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: true,
            PersistTrayEnabled: true,
            UseStartupTrayOnly: false);

        var plan = service.CreateDisplayPlan(preferences, trayAvailable: false);

        Assert.Equal(DesktopStartupDisplayMode.Minimized, plan.DisplayMode);
        Assert.Equal(ShutdownMode.OnLastWindowClose, plan.ShutdownMode);
        Assert.False(plan.ShowActivated);
        Assert.True(plan.ShowInTaskbar);
        Assert.Equal(WindowState.Minimized, plan.WindowState);
        Assert.False(plan.DisableStartupOnlyTrayAfterInitialRestore);
    }

    [Fact]
    public void CreateDisplayPlan_WhenTrayAvailableAndStartupTrayOnly_HidesToTrayAndDisablesTrayAfterRestore()
    {
        var service = CreateService();
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: true,
            PersistTrayEnabled: false,
            UseStartupTrayOnly: true);

        var plan = service.CreateDisplayPlan(preferences, trayAvailable: true);

        Assert.Equal(DesktopStartupDisplayMode.HiddenToTray, plan.DisplayMode);
        Assert.Equal(ShutdownMode.OnExplicitShutdown, plan.ShutdownMode);
        Assert.False(plan.ShowInTaskbar);
        Assert.True(plan.ShowActivated);
        Assert.Equal(WindowState.Normal, plan.WindowState);
        Assert.True(plan.DisableStartupOnlyTrayAfterInitialRestore);
    }

    [Fact]
    public void CreateDisplayPlan_WhenTrayAvailableAndPersistedTrayEnabled_KeepsTrayEnabledAfterRestore()
    {
        var service = CreateService();
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: true,
            PersistTrayEnabled: true,
            UseStartupTrayOnly: false);

        var plan = service.CreateDisplayPlan(preferences, trayAvailable: true);

        Assert.Equal(DesktopStartupDisplayMode.HiddenToTray, plan.DisplayMode);
        Assert.Equal(ShutdownMode.OnExplicitShutdown, plan.ShutdownMode);
        Assert.False(plan.ShowInTaskbar);
        Assert.False(plan.DisableStartupOnlyTrayAfterInitialRestore);
    }

    private static DesktopStartupRuntimeService CreateService()
    {
        return new DesktopStartupRuntimeService(
            getMainWindow: () => throw new NotSupportedException(),
            getTrayIconService: () => new FakeTrayIconService(),
            getTextExpansionService: () => Substitute.For<ITextExpansionService>(),
            getMainWindowViewModel: () => throw new NotSupportedException(),
            getInputSimulatorPool: () => null,
            getPositionProvider: () => null,
            inputSimulatorWarmupService: new InputSimulatorWarmupService());
    }

    private sealed class FakeTrayIconService : ITrayIconService
    {
        public bool IsAvailable { get; set; }

        public List<bool> EnabledCalls { get; } = [];

        public void Initialize()
        {
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void UpdateTooltip(string tooltip)
        {
        }

        public void SetEnabled(bool enabled)
        {
            EnabledCalls.Add(enabled);
        }

        public void Dispose()
        {
        }
    }
}
