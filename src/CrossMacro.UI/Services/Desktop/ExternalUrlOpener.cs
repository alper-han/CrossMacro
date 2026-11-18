
namespace CrossMacro.UI.Services.Desktop;

public sealed class ExternalUrlOpener : IExternalUrlOpener
{
    private readonly Func<ProcessStartInfo, Task<LaunchResult>> _tryStart;
    private readonly Func<string, bool> _commandExists;
    private readonly IRuntimeContext _runtimeContext;

    public ExternalUrlOpener(IRuntimeContext runtimeContext)
        : this(runtimeContext, DesktopProcessLauncher.TryStartAsync, DesktopProcessLauncher.CommandExists) { /* Empty */ }

    internal ExternalUrlOpener(
        IRuntimeContext runtimeContext,
        Func<ProcessStartInfo, Task<LaunchResult>> tryStart,
        Func<string, bool> commandExists)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _tryStart = tryStart ?? throw new ArgumentNullException(nameof(tryStart));
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    }

    public async Task OpenAsync(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !IsSupportedUrl(uri))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS URLs can be opened.", nameof(url));
        }

        await OpenCoreAsync(url).ConfigureAwait(false);
    }

    public async Task OpenAsync(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || !IsSupportedUrl(url))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS URLs can be opened.", nameof(url));
        }

        await OpenCoreAsync(url.AbsoluteUri).ConfigureAwait(false);
    }

    private async Task OpenCoreAsync(string url)
    {
        List<Exception> failures = [];
        if (await DesktopProcessLauncher.TryEachAsync(CreateStartInfos(url, _runtimeContext, _commandExists), _tryStart, failures).ConfigureAwait(false))
        {
            return;
        }

        throw CreateOpenFailedException(failures);
    }

    private static bool IsSupportedUrl(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
    }

    private static IEnumerable<ProcessStartInfo> CreateStartInfos(
        string url,
        IRuntimeContext runtimeContext,
        Func<string, bool> commandExists)
    {
        if (!runtimeContext.IsLinux || runtimeContext.IsFlatpak)
        {
            yield return DesktopProcessLauncher.CreateShellExecute(url);

            yield break;
        }

        foreach (var command in GetLinuxFallbackCommands().Where(command => commandExists(command.FileName)))
        {
            yield return DesktopProcessLauncher.CreateCommand(command, url);
        }
    }

    private static IEnumerable<OpenCommand> GetLinuxFallbackCommands()
    {
        yield return new OpenCommand("xdg-open");
        yield return new OpenCommand("gio", "open");
        yield return new OpenCommand("sensible-browser");
    }

    private static InvalidOperationException CreateOpenFailedException(List<Exception> failures)
    {
        const string message = "Unable to open the URL with the available desktop launchers.";
        return failures.Count is 0
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, new AggregateException(failures));
    }
}
