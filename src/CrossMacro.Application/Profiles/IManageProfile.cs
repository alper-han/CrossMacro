
namespace CrossMacro.Application.Profiles;

public interface IManageProfile
{
    Task<ProfileResult> ListAsync(CancellationToken cancellationToken = default);
    Task<ProfileResult> CurrentAsync(CancellationToken cancellationToken = default);
    Task<ProfileResult> CreateAsync(ProfileRequest request, CancellationToken cancellationToken = default);
    Task<ProfileResult> SwitchAsync(ProfileRequest request, CancellationToken cancellationToken = default);
    Task<ProfileResult> RenameAsync(ProfileRequest request, CancellationToken cancellationToken = default);
    Task<ProfileResult> DeleteAsync(ProfileRequest request, CancellationToken cancellationToken = default);
}
