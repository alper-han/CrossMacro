namespace CrossMacro.Daemon.Contracts.Ipc;

/// <summary>
/// Separates a rejected batch from a lost wire boundary. Only a fully consumed
/// frame can be rejected while keeping the current protocol session alive.
/// </summary>
public sealed class IpcSimulationBatchReadResult
{
    internal IpcSimulationBatchReadResult(IpcSimulationRequest[] events, string? errorMessage, bool hasCompleteFrame)
    {
        Events = Array.AsReadOnly(events);
        ErrorMessage = errorMessage;
        HasCompleteFrame = hasCompleteFrame;
    }

    public IReadOnlyList<IpcSimulationRequest> Events { get; }

    public string? ErrorMessage { get; }

    public bool HasCompleteFrame { get; }

    public bool Success => ErrorMessage is null;
}
