namespace CrossMacro.Platform.Linux.Ipc;

internal interface IInputSimulatorLeaseRefresher
{
    internal Task RefreshLeaseAsync(int screenWidth, int screenHeight, CancellationToken cancellationToken);
}
