namespace CrossMacro.Daemon.Tests.Services;

internal sealed class FakeSecurityService : ISecurityService
{
    private readonly TaskCompletionSource _validated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public (uint Uid, int Pid)? ValidationResult { get; init; }
    public bool DisposeRejectedClient { get; init; }
    public Exception? ValidationException { get; init; }
    public int ValidateCalls { get; private set; }
    public int DisconnectCalls { get; private set; }
    public uint LastDisconnectUid { get; private set; }
    public int LastDisconnectPid { get; private set; }
    public int CaptureStartCalls { get; private set; }
    public int CaptureStopCalls { get; private set; }
    public List<(uint Uid, int Pid, ushort Type, ushort Code, int Value)> SimulationCalls { get; } = [];

    public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client, CancellationToken cancellationToken = default)
    {
        ValidateCalls++;
        _ = _validated.TrySetResult();

        if (ValidationException is not null)
        {
            throw ValidationException;
        }

        if (ValidationResult is null && DisposeRejectedClient)
        {
            client.Dispose();
        }

        return Task.FromResult(ValidationResult);
    }

    public void LogDisconnect(uint uid, int pid, TimeSpan duration)
    {
        DisconnectCalls++;
        LastDisconnectUid = uid;
        LastDisconnectPid = pid;
        _ = _disconnected.TrySetResult();
    }

    public void LogCaptureStart(uint uid, int pid, bool mouse, bool kb)
    {
        CaptureStartCalls++;
    }

    public void LogCaptureStop(uint uid, int pid)
    {
        CaptureStopCalls++;
    }

    public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
    {
        SimulationCalls.Add((uid, pid, type, code, value));
    }

    public Task WaitForValidationAsync(TimeSpan timeout) => _validated.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);

    public Task WaitForDisconnectAsync(TimeSpan timeout) => _disconnected.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);
}
