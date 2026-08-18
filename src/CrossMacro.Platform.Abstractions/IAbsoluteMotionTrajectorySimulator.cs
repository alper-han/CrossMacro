namespace CrossMacro.Platform.Abstractions;

/// <summary>Sends bounded, acknowledged absolute pointer trajectories.</summary>
public interface IAbsoluteMotionTrajectorySimulator
{
    public Task SimulateAbsoluteTrajectoryAsync(
        IReadOnlyList<AbsoluteMotionTrajectorySample> samples,
        CancellationToken cancellationToken = default);
}
