using System;

namespace CrossMacro.UI.Services;

/// <summary>
/// Service for managing system tray icon functionality
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Initialize the tray icon
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Show the tray icon
    /// </summary>
    void Show();
    
    /// <summary>
    /// Hide the tray icon
    /// </summary>
    void Hide();
    
    /// <summary>
    /// Update the tooltip text
    /// </summary>
    void UpdateTooltip(string tooltip);
    
    /// <summary>
    /// Enable or disable tray icon functionality
    /// When disabled, window will close normally instead of minimizing to tray
    /// </summary>
    void SetEnabled(bool enabled);
}
