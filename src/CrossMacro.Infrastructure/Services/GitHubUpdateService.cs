using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

// Source-generated JSON context for trimming-safe deserialization.
[JsonSerializable(typeof(GitHubRelease))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class GitHubJsonContext : JsonSerializerContext
{
}

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
}

public class GitHubUpdateService : IUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/alper-han/CrossMacro/releases/latest";
    private const string UserAgent = "CrossMacro-App";
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(8);
    private readonly IRuntimeContext _runtimeContext;
    private readonly HttpClient? _httpClient;

    public GitHubUpdateService()
        : this(new RuntimeContext(), null)
    {
    }

    public GitHubUpdateService(IRuntimeContext runtimeContext)
        : this(runtimeContext, null)
    {
    }

    public GitHubUpdateService(IRuntimeContext runtimeContext, HttpClient? httpClient)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _httpClient = httpClient;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        if (_runtimeContext.IsFlatpak)
        {
            Log.Information("Running as Flatpak, skipping update check.");
            return new UpdateCheckResult { HasUpdate = false };
        }

        try
        {
            var client = CreateClient();
            var disposeClient = !ReferenceEquals(client, _httpClient);

            try
            {
                ConfigureClient(client);

                using var timeoutCts = new CancellationTokenSource(RequestTimeout);
                using var response = await client.GetAsync(
                    GitHubApiUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                    return new UpdateCheckResult { HasUpdate = false };
                }

                var release = await response.Content.ReadFromJsonAsync(
                    GitHubJsonContext.Default.GitHubRelease,
                    timeoutCts.Token);

                if (release == null)
                {
                    Log.Warning("GitHub release info is null");
                    return new UpdateCheckResult { HasUpdate = false };
                }

                var currentVersion = GetCurrentVersion();
                var tagName = release.TagName?.TrimStart('v');

                Log.Information(
                    "Version Check - Local: {LocalVersion}, Remote Tag: {RemoteTag}, Parsed Remote: {ParsedRemote}",
                    currentVersion,
                    release.TagName,
                    tagName);

                if (currentVersion != null && Version.TryParse(tagName, out var latestVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        Log.Information("Update available: {LatestVersion} > {CurrentVersion}", latestVersion, currentVersion);
                        return new UpdateCheckResult
                        {
                            HasUpdate = true,
                            LatestVersion = tagName ?? release.TagName ?? string.Empty,
                            ReleaseUrl = release.HtmlUrl ?? string.Empty
                        };
                    }

                    Log.Information("No update needed. Local is newer or equal.");
                }
                else
                {
                    Log.Warning("Failed to parse versions. Local: {Local}, Remote: {Remote}", currentVersion, tagName);
                }
            }
            finally
            {
                if (disposeClient)
                {
                    client.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Update check timed out after {TimeoutSeconds:0.##} seconds.", RequestTimeout.TotalSeconds);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Network error while checking updates");
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Failed to deserialize update payload from GitHub");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking for updates");
        }

        return new UpdateCheckResult { HasUpdate = false };
    }

    protected virtual HttpClient CreateClient()
    {
        return _httpClient ?? new HttpClient();
    }

    protected virtual Version? GetCurrentVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version;
    }

    protected virtual TimeSpan RequestTimeout => DefaultRequestTimeout;

    private static void ConfigureClient(HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Any(static ua =>
                string.Equals(ua.Product?.Name, UserAgent, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }
}
