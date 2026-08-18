
namespace CrossMacro.Application.Profiles;

public interface IManageProfile
{
    public Task<ProfileResult> ListAsync(CancellationToken cancellationToken = default);
    public Task<ProfileResult> CurrentAsync(CancellationToken cancellationToken = default);
    public Task<ProfileResult> CreateAsync(ProfileRequest request, CancellationToken cancellationToken = default);
    public Task<ProfileResult> SwitchAsync(ProfileRequest request, CancellationToken cancellationToken = default);
    public Task<ProfileResult> RenameAsync(ProfileRequest request, CancellationToken cancellationToken = default);
    public Task<ProfileResult> DeleteAsync(ProfileRequest request, CancellationToken cancellationToken = default);
}
