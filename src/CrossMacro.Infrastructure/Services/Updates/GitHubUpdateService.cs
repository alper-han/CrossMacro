using System.Net.Http.Json;
using System.Reflection;
using CrossMacro.Core.Services.Updates;

namespace CrossMacro.Infrastructure.Services.Updates;

/// <summary>
/// Checks the latest GitHub release. Pre-release tags are ignored on purpose: the user is only
/// notified about stable releases.
/// </summary>
/// <param name="runtimeContext">Used to skip the check in Flatpak, where updates are managed by the store.</param>
/// <param name="httpClient">Client to use; its <see cref="HttpClient.Timeout"/> bounds the check. A shared 8 second client is used when omitted.</param>
/// <param name="currentVersion">Running version; the entry assembly version when omitted.</param>
public sealed class GitHubUpdateService(
    IRuntimeContext runtimeContext,
    HttpClient? httpClient = null,
    Version? currentVersion = null) : IUpdateService
{
    private static readonly Uri GitHubApiUri = new("https://api.github.com/repos/alper-han/CrossMacro/releases/latest");
    private const string UserAgent = "CrossMacro-App";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    // One process-wide client: creating a client per check leaks sockets until they time out.
    private static readonly HttpClient SharedClient = new() { Timeout = RequestTimeout };

    private readonly IRuntimeContext _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
    private readonly HttpClient _httpClient = httpClient ?? SharedClient;
    private readonly Version? _currentVersion = currentVersion ?? Assembly.GetEntryAssembly()?.GetName().Version;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_runtimeContext.IsFlatpak)
        {
            Log.Information("Running as Flatpak, skipping update check.");
            return NoUpdate();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUri);
            request.Headers.UserAgent.ParseAdd(UserAgent);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                return NoUpdate();
            }

            var release = await response.Content.ReadFromJsonAsync(
                GitHubJsonContext.Default.GitHubRelease,
                cancellationToken).ConfigureAwait(false);

            if (release is null)
            {
                Log.Warning("GitHub release info is null");
                return NoUpdate();
            }

            return Evaluate(release);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Update check timed out after {TimeoutSeconds:0.##} seconds.", _httpClient.Timeout.TotalSeconds);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Network error while checking updates");
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Failed to deserialize update payload from GitHub");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error checking for updates");
        }

        return NoUpdate();
    }

    private UpdateCheckResult Evaluate(GitHubRelease release)
    {
        var tagName = release.TagName?.TrimStart('v');

        Log.Information(
            "Version Check - Local: {LocalVersion}, Remote Tag: {RemoteTag}, Parsed Remote: {ParsedRemote}",
            _currentVersion,
            release.TagName,
            tagName);

        if (tagName is not null && tagName.AsSpan().ContainsAny('-', '+'))
        {
            Log.Information("Ignoring pre-release tag {Tag}", release.TagName);
            return NoUpdate();
        }

        if (_currentVersion is null || !Version.TryParse(tagName, out var latestVersion))
        {
            Log.Warning("Failed to parse versions. Local: {Local}, Remote: {Remote}", _currentVersion, tagName);
            return NoUpdate();
        }

        if (latestVersion <= _currentVersion)
        {
            Log.Information("No update needed. Local is newer or equal.");
            return NoUpdate();
        }

        Log.Information("Update available: {LatestVersion} > {CurrentVersion}", latestVersion, _currentVersion);
        return new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = tagName ?? string.Empty,
            ReleaseUrl = Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releaseUrl)
                ? releaseUrl
                : null,
        };
    }

    private static UpdateCheckResult NoUpdate() => new() { HasUpdate = false };
}
