// CLI tests temporarily replace the process-wide console streams (see ConsoleCapture).
// Keep those global-state tests deterministic under xUnit v3's parallel scheduler.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
