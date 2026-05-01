using System;

namespace CrossMacro.Daemon.Services;

public interface ISessionHandlerFactory
{
    ISessionHandler Create();
}
