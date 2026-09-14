
#pragma warning disable MA0109 // The process boundary accepts string commands and argument arrays; additional Span overloads are not part of this contract.
namespace CrossMacro.Platform.Abstractions.Runtime;

public interface IProcessRunner
{
    public Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default);
    public Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default);
    public Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default);
    public Task WriteClipboardInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default);
    public Task WriteClipboardInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default);
    public Task WriteClipboardInputAndCloseAsync(string command, string[] args, ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default);
    public Task ExecuteCommandAsync(string command, string[] args, CancellationToken cancellationToken = default);
    public Task<string> ReadCommandAsync(string command, string args, CancellationToken cancellationToken = default);
    public Task<string> ReadCommandAsync(string command, string[] args, CancellationToken cancellationToken = default);
}
#pragma warning restore MA0109
