using System;

namespace CrossMacro.Daemon.Services;

public sealed class SessionHandlerFactory : ISessionHandlerFactory
{
    private readonly ISecurityService _security;
    private readonly IVirtualDeviceManager _virtualDevice;
    private readonly IInputCaptureManager _inputCapture;

    public SessionHandlerFactory(
        ISecurityService security,
        IVirtualDeviceManager virtualDevice,
        IInputCaptureManager inputCapture)
    {
        _security = security;
        _virtualDevice = virtualDevice;
        _inputCapture = inputCapture;
    }

    public ISessionHandler Create()
    {
        return new SessionHandler(_security, _virtualDevice, _inputCapture);
    }
}
