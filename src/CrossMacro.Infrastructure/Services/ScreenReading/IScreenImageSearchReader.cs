
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public interface IScreenImageSearchReader
{
    public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame imageTemplate,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions);
}
