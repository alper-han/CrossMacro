namespace CrossMacro.Core.Services;

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public Uri? ReleaseUrl { get; set; }
}
