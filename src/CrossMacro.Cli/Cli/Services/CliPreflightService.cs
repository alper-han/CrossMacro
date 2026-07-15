
namespace CrossMacro.Cli.Services;

public sealed class CliPreflightService : ICliPreflightService
{
    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly Func<IInputCapture> _inputCaptureFactory;
    private readonly Func<bool> _isLinux;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public CliPreflightService(
        IRuntimeContext runtimeContext,
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory)
        : this(
            displaySessionService,
            inputSimulatorFactory,
            inputCaptureFactory,
            () => (runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext))).IsLinux,
            Environment.GetEnvironmentVariable)
    {
    }

    public CliPreflightService(
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        System.Func<bool>? isLinux = null,
        System.Func<string, string?>? getEnvironmentVariable = null)
    {
        _displaySessionService = displaySessionService;
        _inputSimulatorFactory = inputSimulatorFactory;
        _inputCaptureFactory = inputCaptureFactory;
        _isLinux = isLinux ?? OperatingSystem.IsLinux;
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public CliPreflightService(
        IDisplaySessionService displaySessionService,
        IInputSimulator inputSimulator,
        IInputCapture inputCapture,
        System.Func<bool>? isLinux = null,
        System.Func<string, string?>? getEnvironmentVariable = null)
        : this(displaySessionService, () => inputSimulator, () => inputCapture, isLinux, getEnvironmentVariable)
    {
    }

    public Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_displaySessionService.IsSessionSupported(out var sessionReason))
        {
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(sessionReason))
            {
                errors.Add(sessionReason);
            }

            return Task.FromResult(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: display session is not supported.",
                errors));
        }

        if (target is CliPreflightTarget.Headless && _isLinux() && string.IsNullOrWhiteSpace(_getEnvironmentVariable("DISPLAY")) && string.IsNullOrWhiteSpace(_getEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return Task.FromResult(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: no active Linux display session was detected.",
                [
                    "DISPLAY and WAYLAND_DISPLAY are empty.",
                    "Headless mode requires an interactive desktop session.",
                ]));
        }

        if (target is CliPreflightTarget.Play or CliPreflightTarget.Run)
        {
            using var inputSimulator = _inputSimulatorFactory();
            if (!inputSimulator.IsSupported)
            {
                return Task.FromResult(CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: input simulation backend is unavailable.",
                    [$"Input simulator provider is not supported: {inputSimulator.ProviderName}"]));
            }
        }

        if (target is CliPreflightTarget.Record)
        {
            using var inputCapture = _inputCaptureFactory();
            if (!inputCapture.IsSupported)
            {
                return Task.FromResult(CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: input capture backend is unavailable.",
                    [$"Input capture provider is not supported: {inputCapture.ProviderName}"]));
            }
        }

        return Task.FromResult(CliPreflightResult.Ok());
    }
}
