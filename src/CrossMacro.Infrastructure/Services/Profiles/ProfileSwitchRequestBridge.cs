namespace CrossMacro.Infrastructure.Services.Profiles;

/// <summary>Forwards switch requests to the profile coordinator, which is resolved lazily because it depends on the services that issue the requests.</summary>
internal sealed class ProfileSwitchRequestBridge(Func<IProfileSwitchRequestHandler> resolveHandler) : IProfileSwitchRequests
{
    public Task RequestSwitchAsync(string profileId) => resolveHandler().HandleSwitchRequestAsync(profileId);
}
