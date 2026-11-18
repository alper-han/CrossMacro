
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public interface IScreenImageSearchReader : IScreenPixelReader
{
    public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame imageTemplate,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions);
}
