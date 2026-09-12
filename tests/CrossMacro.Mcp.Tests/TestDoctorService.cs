namespace CrossMacro.Mcp.Tests;

internal sealed class TestDoctorService(DoctorReport report) : IDoctorService
{
        private readonly DoctorReport _report = report;

        public bool WasRun { get; private set; }

        public Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken)
        {
            Assert.False(verbose);
            cancellationToken.ThrowIfCancellationRequested();
            WasRun = true;
            return Task.FromResult(_report);
        }
    }
