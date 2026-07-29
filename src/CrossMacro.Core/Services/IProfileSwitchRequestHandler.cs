namespace CrossMacro.Core.Services;

public interface IProfileSwitchRequestHandler
{
    public Task HandleSwitchRequestAsync(string profileId);
}
