
namespace CrossMacro.UI.Services;

/// <summary>
/// Service for managing system tray icon functionality
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Returns true when tray functionality is operational in the current session.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Initialize the tray icon
    /// </summary>
    public void Initialize();

    /// <summary>
    /// Show the tray icon
    /// </summary>
    public void Show();

    /// <summary>
    /// Hide the tray icon
    /// </summary>
    public void Hide();

    /// <summary>
    /// Update the tooltip text
    /// </summary>
    public void UpdateTooltip(string tooltip);

    /// <summary>
    /// Enable or disable tray icon functionality
    /// When disabled, window will close normally instead of minimizing to tray
    /// </summary>
    public void SetEnabled(bool enabled);
}
