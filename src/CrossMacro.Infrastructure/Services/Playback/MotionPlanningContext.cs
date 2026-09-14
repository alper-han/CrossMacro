namespace CrossMacro.Infrastructure.Services.Playback;

internal readonly record struct MotionPlanningContext(
    IInputSimulator? Simulator, MacroEventExecutor? Executor, IPlaybackCoordinator? Coordinator);
