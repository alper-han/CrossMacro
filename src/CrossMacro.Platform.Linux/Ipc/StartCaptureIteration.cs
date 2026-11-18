namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>One pass of the start loop: the plan, and whether the request is finished (a stop was sent).</summary>
internal readonly record struct StartCaptureIteration(StartCapturePlan Plan, bool ShouldStop);
