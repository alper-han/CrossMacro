// CLI tests temporarily replace process-wide console and core logging sinks.
// Keep those global-state tests deterministic under xUnit v3's parallel scheduler.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
