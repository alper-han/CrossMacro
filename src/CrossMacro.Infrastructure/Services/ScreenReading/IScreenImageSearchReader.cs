
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public interface IScreenImageSearchReader
{
    Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame template,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions);
}
