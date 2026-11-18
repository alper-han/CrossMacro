
namespace CrossMacro.Infrastructure.Tests.Services.Playback;

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

    [Theory]
    [InlineData(MacroMouseButton.None)]
    [InlineData(MacroMouseButton.ScrollUp)]
    [InlineData(MacroMouseButton.ScrollDown)]
    [InlineData(MacroMouseButton.ScrollLeft)]
    [InlineData(MacroMouseButton.ScrollRight)]
    public void Map_ShouldThrow_ForButtonsThatAreNotClickable(MacroMouseButton button)
    {
        var act = () => _mapper.Map(button);

        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Map_ShouldThrow_ForUndefinedButtonValue()
    {
        var act = () => _mapper.Map((MacroMouseButton)999);

        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
