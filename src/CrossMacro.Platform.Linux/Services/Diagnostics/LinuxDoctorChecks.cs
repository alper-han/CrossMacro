using System.Text.Json.Nodes;

namespace CrossMacro.Platform.Linux.Services.Diagnostics;

/// <summary>The Linux doctor checks: screen-reading readiness, display variables, input access and screen-reading backends.</summary>
internal sealed class LinuxDoctorChecks(
    LinuxRuntimeContext runtimeContext,
    IEnvironmentInfoProvider environmentInfoProvider,
    ILinuxInputDiagnostics inputDiagnostics,
    IScreenReadingDiagnosticProvider screenReadingDiagnostics,
    IScreenReadingCapabilityReadiness screenReadingReadiness) : IPlatformDoctorChecks
{
    public async Task<DoctorCheck?> PrepareAsync(bool verbose, CancellationToken cancellationToken)
    {
        try
        {
            await screenReadingReadiness.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DoctorCheck
            {
                Name = "linux-screen-reading-initialization",
                Status = DoctorCheckStatus.Warn,
                Message = "Screen-reading initialization failed; capability checks may be incomplete.",
                Details = verbose ? new JsonObject { ["error"] = ex.Message } : null,
            };
        }
    }

    public async Task<IReadOnlyList<DoctorCheck>> CheckAsync(
        IReadOnlyList<DoctorCheck> capabilityChecks,
        bool verbose,
        CancellationToken cancellationToken)
    {
        var state = await BuildInputStateAsync(cancellationToken).ConfigureAwait(false);
        var checks = new List<DoctorCheck> { BuildDisplayVariablesCheck(verbose) };
        checks.AddRange(LinuxInputDoctorChecks.Build(state, capabilityChecks, verbose));
        checks.Add(BuildScreenReadingCheck(verbose));
        return checks;
    }

    private DoctorCheck BuildScreenReadingCheck(bool verbose)
    {
        var display = screenReadingDiagnostics.GetSnapshot().ToDisplay();

        return new DoctorCheck
        {
            Name = "linux-screen-reading",
            Status = display.HasSelectedBackend ? DoctorCheckStatus.Pass : DoctorCheckStatus.Warn,
            Message = display.Message,
            Details = verbose
                ? new JsonObject
                {
                    ["supportedSession"] = display.IsSupportedSession,
                    ["sessionKind"] = display.SessionKind,
                    ["policyName"] = display.PolicyName,
                    ["policyOrder"] = new JsonArray(display.PolicyOrder.Select(x => (JsonNode)x).ToArray()),
                    ["selectedBackend"] = display.SelectedBackend,
                    ["failureBackend"] = display.FailureBackend,
                    ["failureKind"] = display.FailureKind,
                    ["failureMessage"] = display.FailureMessage,
                    ["remediation"] = display.Remediation,
                    ["selectedBackendDetails"] = display.SelectedBackendDetails,
                    ["backends"] = new JsonArray(display.Backends.Select(backend => (JsonNode)new JsonObject
                    {
                        ["backend"] = backend.Backend,
                        ["available"] = backend.IsAvailable,
                        ["errorKind"] = backend.ErrorKind,
                        ["errorMessage"] = backend.ErrorMessage,
                        ["details"] = backend.Details,
                    }).ToArray()),
                }
                : null,
        };
    }

    private DoctorCheck BuildDisplayVariablesCheck(bool verbose)
    {
        var xdgSessionType = runtimeContext.SessionType;
        var display = runtimeContext.Display;
        var waylandDisplay = runtimeContext.WaylandDisplay;

        var hasAnyDisplayVariable = !string.IsNullOrWhiteSpace(display) || !string.IsNullOrWhiteSpace(waylandDisplay);
        var hasSessionType = !string.IsNullOrWhiteSpace(xdgSessionType);

        var status = hasAnyDisplayVariable && hasSessionType
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        return new DoctorCheck
        {
            Name = "linux-display-vars",
            Status = status,
            Message = status is DoctorCheckStatus.Pass
                ? "Linux display session variables look healthy."
                : "Some Linux display variables are missing; remote/SSH playback may fail.",
            Details = verbose
                ? new JsonObject { ["xdgSessionType"] = xdgSessionType, ["display"] = display, ["waylandDisplay"] = waylandDisplay }
                : null,
        };
    }

    private async Task<LinuxInputState> BuildInputStateAsync(CancellationToken cancellationToken)
    {
        var input = await inputDiagnostics.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var sessionType = runtimeContext.SessionType;
        var detectedEnvironment = environmentInfoProvider.CurrentEnvironment;

        var isWayland = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase)
            || detectedEnvironment is DisplayEnvironment.LinuxWayland
                or DisplayEnvironment.LinuxHyprland
                or DisplayEnvironment.LinuxWayfire
                or DisplayEnvironment.LinuxKDE
                or DisplayEnvironment.LinuxGnome;

        var isX11 = string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase)
            || detectedEnvironment is DisplayEnvironment.LinuxX11;

        return new LinuxInputState(sessionType, isWayland, isX11, runtimeContext.IsFlatpak, input);
    }
}
