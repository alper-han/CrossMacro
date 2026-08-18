
namespace CrossMacro.Daemon.Services;

internal sealed class SessionHandlerFactory(
    ISecurityService security,
    IVirtualDeviceManager virtualDevice,
    IInputCaptureManager inputCapture) : ISessionHandlerFactory
{
    private readonly ISecurityService _security = security;
    private readonly IVirtualDeviceManager _virtualDevice = virtualDevice;
    private readonly IInputCaptureManager _inputCapture = inputCapture;

    public ISessionHandler Create()
    {
        return new SessionHandler(_security, _virtualDevice, _inputCapture);
    }
}
