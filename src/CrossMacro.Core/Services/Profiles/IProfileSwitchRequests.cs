namespace CrossMacro.Core.Services.Profiles;

public interface IProfileSwitchRequests
{
    public Task RequestSwitchAsync(string profileId);
}
