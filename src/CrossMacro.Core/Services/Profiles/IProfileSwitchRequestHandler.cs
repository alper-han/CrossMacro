namespace CrossMacro.Core.Services.Profiles;

public interface IProfileSwitchRequestHandler
{
    public Task HandleSwitchRequestAsync(string profileId);
}
