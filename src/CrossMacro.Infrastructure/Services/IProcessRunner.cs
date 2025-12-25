using System.Diagnostics;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Services;

public interface IProcessRunner
{
    Task<bool> CheckCommandAsync(string command);
    Task RunCommandAsync(string command, string args, string input);
    Task ExecuteCommandAsync(string command, string[] args);
    Task<string> ReadCommandAsync(string command, string args);
}
