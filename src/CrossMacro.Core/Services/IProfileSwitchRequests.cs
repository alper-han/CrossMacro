namespace CrossMacro.Core.Services;

public interface IProfileSwitchRequests
{
    public Task RequestSwitchAsync(string profileId);
}
