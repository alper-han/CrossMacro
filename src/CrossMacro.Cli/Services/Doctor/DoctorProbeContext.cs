namespace CrossMacro.Cli.Services.Doctor;

internal sealed record DoctorProbeContext(
    Func<IInputSimulator> InputSimulatorFactory,
    Func<IInputCapture> InputCaptureFactory,
    IMousePositionProvider MousePositionProvider);
