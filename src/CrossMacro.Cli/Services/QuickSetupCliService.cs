namespace CrossMacro.Cli.Services;

public sealed class QuickSetupCliService(
    IEnumerable<IFlatpakQuickSetupService> flatpakServices,
    IEnumerable<IAppImageQuickSetupService> appImageServices) : IQuickSetupCliService
{
    private readonly IReadOnlyList<IFlatpakQuickSetupService> _flatpakServices = [.. flatpakServices];
    private readonly IReadOnlyList<IAppImageQuickSetupService> _appImageServices = [.. appImageServices];

    public QuickSetupStatus GetStatus()
    {
        var flatpak = _flatpakServices.FirstOrDefault(static service => service.IsApplicable());
        if (flatpak is not null)
        {
            return new QuickSetupStatus(Applicable: true, Provider: "flatpak", ShouldPrompt: false);
        }

        var appImage = _appImageServices.FirstOrDefault(static service => service.IsApplicable());
        return appImage is null
            ? new QuickSetupStatus(Applicable: false, Provider: "none", ShouldPrompt: false)
            : new QuickSetupStatus(Applicable: true, Provider: "appimage", ShouldPrompt: appImage.ShouldPrompt());
    }

    public async Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken)
    {
        var flatpak = _flatpakServices.FirstOrDefault(static service => service.IsApplicable());
        if (flatpak is not null)
        {
            return new QuickSetupCliResult(
                Applicable: true,
                Provider: "flatpak",
                Result: await flatpak.RunAsync(cancellationToken).ConfigureAwait(false));
        }

        var appImage = _appImageServices.FirstOrDefault(static service => service.IsApplicable());
        if (appImage is not null)
        {
            return new QuickSetupCliResult(
                Applicable: true,
                Provider: "appimage",
                Result: await appImage.RunAsync(cancellationToken).ConfigureAwait(false));
        }

        return new QuickSetupCliResult(
            Applicable: false,
            Provider: "none",
            Result: new QuickSetupResult(
                Success: false,
                Message: "Temporary input setup is available only for Flatpak or AppImage Wayland sessions."));
    }
}
