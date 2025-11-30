using System;
using System;
using System.Threading.Tasks;
using CrossMacro.Core.Wayland;

namespace CrossMacro.Infrastructure.Wayland
{
    /// <summary>
    /// Fallback position provider using "Corner Reset" hack
    /// TODO: Implement position tracking via corner reset
    /// </summary>
    public class FallbackPositionProvider : IMousePositionProvider
    {
        public string ProviderName => "None (Relative Only)";
        public bool IsSupported => false;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            return Task.FromResult<(int X, int Y)?>(null);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            return Task.FromResult<(int Width, int Height)?>(null);
        }



        public void Dispose() { }
    }

}
