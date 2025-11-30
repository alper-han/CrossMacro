using System;
using System.Threading.Tasks;

namespace CrossMacro.Core.Wayland
{
    /// <summary>
    /// Interface for mouse position providers across different Wayland compositors
    /// </summary>
    public interface IMousePositionProvider : IDisposable
    {
        /// <summary>
        /// Name of the position provider (e.g., "Hyprland IPC", "KDE DBus")
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// Whether this provider is supported on the current system
        /// </summary>
        bool IsSupported { get; }
        
        /// <summary>
        /// Get the current absolute mouse position asynchronously
        /// </summary>
        /// <returns>Tuple of (X, Y) coordinates, or null if unavailable</returns>
        Task<(int X, int Y)?> GetAbsolutePositionAsync();
        
        /// <summary>
        /// Get the screen resolution asynchronously
        /// </summary>
        /// <returns>Tuple of (Width, Height), or null if unavailable</returns>
        Task<(int Width, int Height)?> GetScreenResolutionAsync();
    }
}
