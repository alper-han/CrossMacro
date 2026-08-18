
namespace CrossMacro.Infrastructure.Services.Playback;

public readonly record struct ImageClickMovementResolution(
    bool IsSuccess,
    MouseCoordinateMode CoordinateMode,
    int X,
    int Y,
    string? ErrorMessage)
{
    public static ImageClickMovementResolution Absolute(ScreenPoint target) =>
        new(IsSuccess: true, MouseCoordinateMode.Absolute, target.X, target.Y, ErrorMessage: null);

    public static ImageClickMovementResolution Relative(int deltaX, int deltaY) =>
        new(IsSuccess: true, MouseCoordinateMode.Relative, deltaX, deltaY, ErrorMessage: null);

    public static ImageClickMovementResolution Failure(string message) =>
        new(IsSuccess: false, default, 0, 0, message);
}
