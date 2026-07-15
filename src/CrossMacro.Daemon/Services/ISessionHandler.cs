
namespace CrossMacro.Daemon.Services;

public interface ISessionHandler
{
    /// <summary>
    /// Runs the session loop for the given client socket.
    /// </summary>
    public Task RunAsync(Socket client, uint uid, int pid, CancellationToken token);
}
