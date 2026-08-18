namespace CrossMacro.Cli.Services.Doctor;

internal sealed class DoctorCapabilityProbeGroup(DoctorProbeContext context) : IDoctorProbeGroup
{
    private readonly DoctorProbeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public IReadOnlyList<DoctorCheck> Run(bool verbose)
    {
        return
        [
            BuildInputSimulationCheck(verbose),
            BuildInputCaptureCheck(verbose),
            BuildPositionProviderCheck(verbose),
        ];
    }

    private DoctorCheck BuildInputSimulationCheck(bool verbose)
    {
        try
        {
            using var simulator = _context.InputSimulatorFactory();
            var isSupported = simulator.IsSupported;

            return new DoctorCheck
            {
                Name = "input-simulator",
                Status = isSupported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = isSupported
                    ? $"Input simulator backend is available ({simulator.ProviderName})."
                    : $"Input simulator backend is unavailable ({simulator.ProviderName}).",
                Details = verbose
                    ? new JsonObject
                    {
                        ["provider"] = simulator.ProviderName,
                        ["supported"] = isSupported,
                    }
                    : null,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DoctorCheck
            {
                Name = "input-simulator",
                Status = DoctorCheckStatus.Fail,
                Message = "Input simulator backend probe failed.",
                Details = verbose ? new JsonObject { ["error"] = ex.Message } : null,
            };
        }
    }

    private DoctorCheck BuildInputCaptureCheck(bool verbose)
    {
        try
        {
            using var capture = _context.InputCaptureFactory();
            var isSupported = capture.IsSupported;

            return new DoctorCheck
            {
                Name = "input-capture",
                Status = isSupported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = isSupported
                    ? $"Input capture backend is available ({capture.ProviderName})."
                    : $"Input capture backend is unavailable ({capture.ProviderName}).",
                Details = verbose
                    ? new JsonObject
                    {
                        ["provider"] = capture.ProviderName,
                        ["supported"] = isSupported,
                    }
                    : null,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DoctorCheck
            {
                Name = "input-capture",
                Status = DoctorCheckStatus.Fail,
                Message = "Input capture backend probe failed.",
                Details = verbose ? new JsonObject { ["error"] = ex.Message } : null,
            };
        }
    }

    private DoctorCheck BuildPositionProviderCheck(bool verbose)
    {
        var provider = _context.MousePositionProvider;
        var isSupported = provider.IsSupported;

        return new DoctorCheck
        {
            Name = "position-provider",
            Status = isSupported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Warn,
            Message = isSupported
                ? $"Position provider is available ({provider.ProviderName})."
                : $"Position provider is unavailable ({provider.ProviderName}); absolute replay may downgrade to fallback mode.",
            Details = verbose
                ? new JsonObject
                {
                    ["provider"] = provider.ProviderName,
                    ["supported"] = isSupported,
                }
                : null,
        };
    }
}
