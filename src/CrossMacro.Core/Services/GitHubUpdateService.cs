using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Serilog;

namespace CrossMacro.Core.Services;

// Source-generated JSON context for trimming-safe deserialization
[JsonSerializable(typeof(GitHubRelease))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class GitHubJsonContext : JsonSerializerContext { }

// Must be public or internal for source generator to access
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

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        // Skip update check if running as Flatpak
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")))
        {
            Log.Information("Running as Flatpak, skipping update check.");
            return new UpdateCheckResult { HasUpdate = false };
        }

        try
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            var response = await client.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                return new UpdateCheckResult { HasUpdate = false };
            }

            var release = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubRelease);
            if (release == null)
            {
                Log.Warning("GitHub release info is null");
                return new UpdateCheckResult { HasUpdate = false };
            }

            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            var tagName = release.TagName?.TrimStart('v');
            
            Log.Information("Version Check - Local: {LocalVersion}, Remote Tag: {RemoteTag}, Parsed Remote: {ParsedRemote}", 
                currentVersion, release.TagName, tagName);

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
                else
                {
                    Log.Information("No update needed. Local is newer or equal.");
                }
            }
            else
            {
                Log.Warning("Failed to parse versions. Local: {Local}, Remote: {Remote}", currentVersion, tagName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking for updates");
        }

        return new UpdateCheckResult { HasUpdate = false };
    }
    protected virtual HttpClient CreateClient()
    {
        return new HttpClient();
    }
}
