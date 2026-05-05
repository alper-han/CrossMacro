using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Services;

public interface IProcessRunner
{
    Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default);
    Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default);
    Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default);
    Task WriteInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default);
    Task WriteInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default);
    Task ExecuteCommandAsync(string command, string[] args, CancellationToken cancellationToken = default);
    Task<string> ReadCommandAsync(string command, string args, CancellationToken cancellationToken = default);
    Task<string> ReadCommandAsync(string command, string[] args, CancellationToken cancellationToken = default);
}
