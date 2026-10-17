
namespace CrossMacro.Cli.Services.Runtime;

public sealed class CliPreflightService(
    IDisplaySessionService displaySessionService,
    Func<IInputSimulator> inputSimulatorFactory,
    Func<IInputCapture> inputCaptureFactory,
    System.Func<bool>? isLinux = null,
    System.Func<string, string?>? getEnvironmentVariable = null,
    System.Func<bool>? isWindowTaskMode = null) : ICliPreflightService
{
    private readonly IDisplaySessionService _displaySessionService = displaySessionService;
    private readonly Func<IInputSimulator> _inputSimulatorFactory = inputSimulatorFactory;
    private readonly Func<IInputCapture> _inputCaptureFactory = inputCaptureFactory;
    private readonly Func<bool> _isLinux = isLinux ?? OperatingSystem.IsLinux;
    private readonly Func<string, string?> _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    private readonly Func<bool> _isWindowTaskMode = isWindowTaskMode ?? (() => WindowTaskMode.Enabled);

    public CliPreflightService(
        IRuntimeContext runtimeContext,
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory)
        : this(
            displaySessionService,
            inputSimulatorFactory,
            inputCaptureFactory,
            () => IsLinuxRuntime(runtimeContext),
            Environment.GetEnvironmentVariable)
    { /* Empty */ }

    public CliPreflightService(
        IDisplaySessionService displaySessionService,
        IInputSimulator inputSimulator,
        IInputCapture inputCapture,
        System.Func<bool>? isLinux = null,
        System.Func<string, string?>? getEnvironmentVariable = null)
        : this(displaySessionService, () => inputSimulator, () => inputCapture, isLinux, getEnvironmentVariable) { /* Empty */ }

    private static bool IsLinuxRuntime(IRuntimeContext? runtimeContext)
    {
        return (runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext))).IsLinux;
    }

    public async Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionSupport = await _displaySessionService.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
        if (!sessionSupport.Supported)
        {
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(sessionSupport.Reason))
            {
                errors.Add(sessionSupport.Reason);
            }

            return CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: display session is not supported.",
                errors);
        }

        if (target is CliPreflightTarget.Headless && _isLinux() && string.IsNullOrWhiteSpace(_getEnvironmentVariable("DISPLAY")) && string.IsNullOrWhiteSpace(_getEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: no active Linux display session was detected.",
                [
                    "DISPLAY and WAYLAND_DISPLAY are empty.",
                    "Headless mode requires an interactive desktop session.",
                ]);
        }

        // External-only scripts do not need a desktop input device. Playback
        // still requests one when the macro contains native input, and the
        // macOS window-task factory continues to reject that request.
        if ((target is CliPreflightTarget.Play or CliPreflightTarget.Run) && !_isWindowTaskMode())
        {
            using var inputSimulator = _inputSimulatorFactory();
            if (!inputSimulator.IsSupported)
            {
                    return CliPreflightResult.Fail(
                        CliExitCode.EnvironmentError,
                        "Preflight check failed: input simulation backend is unavailable.",
                        [$"Input simulator provider is not supported: {inputSimulator.ProviderName}"]);
            }
        }

        if (target is CliPreflightTarget.Record)
        {
            using var inputCapture = _inputCaptureFactory();
            if (!inputCapture.IsSupported)
            {
                    return CliPreflightResult.Fail(
                        CliExitCode.EnvironmentError,
                        "Preflight check failed: input capture backend is unavailable.",
                        [$"Input capture provider is not supported: {inputCapture.ProviderName}"]);
            }
        }

        return CliPreflightResult.Ok();
    }
}
