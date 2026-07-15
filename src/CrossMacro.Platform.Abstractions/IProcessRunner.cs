
namespace CrossMacro.Platform.Abstractions;

public interface IProcessRunner
{
    Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default);
    Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default);
    Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default);
    Task WriteClipboardInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default);
    Task WriteClipboardInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default);
    Task WriteClipboardInputAndCloseAsync(string command, string[] args, ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default);
    Task ExecuteCommandAsync(string command, string[] args, CancellationToken cancellationToken = default);
    Task<string> ReadCommandAsync(string command, string args, CancellationToken cancellationToken = default);
    Task<string> ReadCommandAsync(string command, string[] args, CancellationToken cancellationToken = default);
}
