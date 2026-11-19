namespace CrossMacro.Cli.Tests;

/// <summary>
/// Redirects the process-wide console streams to in-memory writers. The assembly runs
/// non-parallel (<see cref="TestAssemblyConfiguration"/>), so at most one capture is active.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public ConsoleCapture()
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
    }

    public string StandardOutput => _stdout.ToString();

    public string StandardError => _stderr.ToString();

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _stdout.Dispose();
        _stderr.Dispose();
    }
}
