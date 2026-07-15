using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

public sealed class ImageClickMovementResolver : IImageClickMovementResolver
{
    private readonly IMousePositionProvider? _mousePositionProvider;

    public ImageClickMovementResolver(IMousePositionProvider? mousePositionProvider)
    {
        _mousePositionProvider = mousePositionProvider;
    }

    public async Task<ImageClickMovementResolution> ResolveAsync(
        IInputSimulator inputSimulator,
        ScreenPoint target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        cancellationToken.ThrowIfCancellationRequested();

        if (inputSimulator is not IInputSimulatorCapabilities { SupportsAbsoluteCoordinates: false })
        {
            return ImageClickMovementResolution.Absolute(target);
        }

        if (_mousePositionProvider is null || !_mousePositionProvider.IsSupported)
        {
            return ImageClickMovementResolution.Failure(
                "No supported IMousePositionProvider is available for relative movement.");
        }

        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        if (position is null)
        {
            return ImageClickMovementResolution.Failure(
                "The current mouse position is unavailable for relative movement.");
        }

        try
        {
            return ImageClickMovementResolution.Relative(
                checked(target.X - position.Value.X),
                checked(target.Y - position.Value.Y));
        }
        catch (OverflowException)
        {
            return ImageClickMovementResolution.Failure(
                "The target and current mouse positions cannot be represented as a relative movement.");
        }
    }
}
