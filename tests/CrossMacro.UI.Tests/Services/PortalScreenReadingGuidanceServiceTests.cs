
namespace CrossMacro.UI.Tests.Services;

public sealed class PortalScreenReadingGuidanceServiceTests
{
    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WhenPortalSelectedAndNoRestoreToken_ShowsGuidance()
    {
        var dialog = new RecordingDialogService();
        var service = CreateService(dialog, selectedBackend: "Portal", restoreToken: null);

        await service.ShowBeforePortalWarmupAsync();

        var call = Assert.Single(dialog.MessageCalls);
        Assert.Equal(UIStrings.PortalScreenReadingGuidanceTitle, call.Title);
        Assert.Equal(UIStrings.PortalScreenReadingGuidanceMessage, call.Message);
        Assert.Equal(UIStrings.ContinueButton, call.ButtonText);
    }

    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WhenRestoreTokenIsWhitespace_ShowsGuidance()
    {
        var dialog = new RecordingDialogService();
        var service = CreateService(dialog, selectedBackend: "Portal", restoreToken: "   ");

        await service.ShowBeforePortalWarmupAsync();

        _ = Assert.Single(dialog.MessageCalls);
    }

    [Theory]
    [InlineData("WlrScreencopy")]
    [InlineData("ExtImageCopy")]
    [InlineData("KWinScreenShot2")]
    [InlineData("X11")]
    [InlineData("portal")]
    [InlineData(null)]
    public async Task ShowBeforePortalWarmupAsync_WhenBackendIsNotOrdinalPortal_DoesNotShowGuidance(string? selectedBackend)
    {
        var dialog = new RecordingDialogService();
        var service = CreateService(dialog, selectedBackend, restoreToken: null);

        await service.ShowBeforePortalWarmupAsync();

        Assert.Empty(dialog.MessageCalls);
    }

    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WhenRestoreTokenExists_DoesNotShowGuidance()
    {
        var dialog = new RecordingDialogService();
        var service = CreateService(dialog, selectedBackend: "Portal", restoreToken: "restore-token");

        await service.ShowBeforePortalWarmupAsync();

        Assert.Empty(dialog.MessageCalls);
    }

    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WhenDiagnosticsFail_DoesNotShowGuidance()
    {
        var dialog = new RecordingDialogService();
        var service = new PortalScreenReadingGuidanceService(
            dialog,
            new StaticSettingsService(restoreToken: null),
            new ThrowingDiagnosticProvider());

        await service.ShowBeforePortalWarmupAsync();

        Assert.Empty(dialog.MessageCalls);
    }

    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WhenNoDiagnosticsRegistered_DoesNotShowGuidance()
    {
        var dialog = new RecordingDialogService();
        var service = new PortalScreenReadingGuidanceService(
            dialog,
            new StaticSettingsService(restoreToken: null));

        await service.ShowBeforePortalWarmupAsync();

        Assert.Empty(dialog.MessageCalls);
    }

    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WhenCalledRepeatedly_ShowsGuidanceOncePerServiceInstance()
    {
        var dialog = new RecordingDialogService();
        var service = CreateService(dialog, selectedBackend: "Portal", restoreToken: null);

        await service.ShowBeforePortalWarmupAsync();
        await service.ShowBeforePortalWarmupAsync();

        _ = Assert.Single(dialog.MessageCalls);
    }

    [Fact]
    public async Task ShowBeforePortalWarmupAsync_WaitsForCapabilityReadinessBeforeShowingPortalGuidance()
    {
        var dialog = new RecordingDialogService();
        var diagnostics = new MutableDiagnosticProvider("Portal");
        var readiness = new RecordingCapabilityReadiness(() => diagnostics.SelectedBackend = "GnomeExtension");
        var service = new PortalScreenReadingGuidanceService(
            dialog,
            new StaticSettingsService(restoreToken: null),
            diagnostics,
            readiness);

        await service.ShowBeforePortalWarmupAsync();

        Assert.Equal(1, readiness.Calls);
        Assert.Empty(dialog.MessageCalls);
    }

    [Fact]
    public void PortalGuidanceMessage_ExplainsPortalSelectionWithoutClaimingControl()
    {
        const string message = UIStrings.PortalScreenReadingGuidanceMessage;

        Assert.Contains("system screen-sharing portal dialog next", message, StringComparison.Ordinal);
        Assert.Contains("monitor or screen sources", message, StringComparison.Ordinal);
        Assert.Contains("select every monitor", message, StringComparison.Ordinal);
        Assert.Contains("cannot choose or force", message, StringComparison.Ordinal);
        Assert.Contains("saved permission may be reused", message, StringComparison.Ordinal);
    }

    private static PortalScreenReadingGuidanceService CreateService(
        RecordingDialogService dialog,
        string? selectedBackend,
        string? restoreToken)
    {
        return new PortalScreenReadingGuidanceService(
            dialog,
            new StaticSettingsService(restoreToken),
            new StaticDiagnosticProvider(selectedBackend));
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public List<(string Title, string Message, string ButtonText)> MessageCalls { get; } = [];

        public Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
        {
            throw new NotSupportedException();
        }

        public Task ShowMessageAsync(string title, string message, string buttonText = "OK")
        {
            MessageCalls.Add((title, message, buttonText));
            return Task.CompletedTask;
        }

        public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters)
        {
            throw new NotSupportedException();
        }

        public Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticSettingsService : ISettingsService
    {
        public StaticSettingsService(string? restoreToken)
        {
            Current = new AppSettings { PortalScreenCastRestoreToken = restoreToken };
        }

        public AppSettings Current { get; }

        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

        public AppSettings Load() => Current;

        public Task SaveAsync() => Task.CompletedTask;

        public void Save()
        {
        }
    }

    private sealed class StaticDiagnosticProvider(string? selectedBackend) : IScreenReadingDiagnosticProvider
    {
        private readonly string? _selectedBackend = selectedBackend;

        public ScreenReadingDiagnosticSnapshot GetSnapshot()
        {
            return new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Wayland",
                PolicyName: "test",
                PolicyOrder: ["Portal"],
                SelectedBackend: _selectedBackend,
                Backends: [],
                FailureBackend: null,
                FailureKind: null,
                FailureMessage: null,
                Remediation: null);
        }
    }

    private sealed class MutableDiagnosticProvider(string? selectedBackend) : IScreenReadingDiagnosticProvider
    {
        public string? SelectedBackend { get; set; } = selectedBackend;

        public ScreenReadingDiagnosticSnapshot GetSnapshot()
        {
            return new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Wayland",
                PolicyName: "test",
                PolicyOrder: ["Portal"],
                SelectedBackend,
                Backends: [],
                FailureBackend: null,
                FailureKind: null,
                FailureMessage: null,
                Remediation: null);
        }
    }

    private sealed class RecordingCapabilityReadiness(Action onEnsure) : IScreenReadingCapabilityReadiness
    {
        public int Calls { get; private set; }

        public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            onEnsure();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDiagnosticProvider : IScreenReadingDiagnosticProvider
    {
        public ScreenReadingDiagnosticSnapshot GetSnapshot()
        {
            throw new InvalidOperationException("diagnostics failed");
        }
    }
}
