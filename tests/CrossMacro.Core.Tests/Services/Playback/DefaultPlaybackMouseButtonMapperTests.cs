
namespace CrossMacro.Core.Tests.Services.Playback;

public sealed class DefaultPlaybackMouseButtonMapperTests
{
    private readonly DefaultPlaybackMouseButtonMapper _mapper;

    public DefaultPlaybackMouseButtonMapperTests()
    {
        _mapper = new DefaultPlaybackMouseButtonMapper();
    }

    [Theory]
    [InlineData(MacroMouseButton.Left, MouseButtonCode.Left)]
    [InlineData(MacroMouseButton.Right, MouseButtonCode.Right)]
    [InlineData(MacroMouseButton.Middle, MouseButtonCode.Middle)]
    [InlineData(MacroMouseButton.Side1, MouseButtonCode.Side1)]
    [InlineData(MacroMouseButton.Side2, MouseButtonCode.Side2)]
    public void Map_ShouldReturnCorrectCode_ForKnownButtons(MacroMouseButton button, int expectedCode)
    {
        var result = _mapper.Map(button);
        _ = result.Should().Be(expectedCode);
    }

    [Fact]
    public void Map_ShouldReturnLeftClick_ForUnknownButton()
    {
        // MacroMouseButton.None or any other unhandled value should default to Left
        var result = _mapper.Map(MacroMouseButton.None);
        _ = result.Should().Be(MouseButtonCode.Left);
    }

    [Theory]
    [InlineData(MacroMouseButton.ScrollUp)]
    [InlineData(MacroMouseButton.ScrollDown)]
    [InlineData(MacroMouseButton.ScrollLeft)]
    [InlineData(MacroMouseButton.ScrollRight)]
    public void Map_ShouldReturnLeftClick_ForScrollButtons(MacroMouseButton scrollButton)
    {
        // Scroll buttons are not mappable to button codes, should default to Left
        var result = _mapper.Map(scrollButton);
        _ = result.Should().Be(MouseButtonCode.Left);
    }
}
