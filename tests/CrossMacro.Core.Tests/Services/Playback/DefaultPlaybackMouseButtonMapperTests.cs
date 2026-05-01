using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Playback;

public class DefaultPlaybackMouseButtonMapperTests
{
    private readonly DefaultPlaybackMouseButtonMapper _mapper;

    public DefaultPlaybackMouseButtonMapperTests()
    {
        _mapper = new DefaultPlaybackMouseButtonMapper();
    }

    [Theory]
    [InlineData(MouseButton.Left, MouseButtonCode.Left)]
    [InlineData(MouseButton.Right, MouseButtonCode.Right)]
    [InlineData(MouseButton.Middle, MouseButtonCode.Middle)]
    [InlineData(MouseButton.Side1, MouseButtonCode.Side1)]
    [InlineData(MouseButton.Side2, MouseButtonCode.Side2)]
    public void Map_ShouldReturnCorrectCode_ForKnownButtons(MouseButton button, int expectedCode)
    {
        var result = _mapper.Map(button);
        result.Should().Be(expectedCode);
    }

    [Fact]
    public void Map_ShouldReturnLeftClick_ForUnknownButton()
    {
        // MouseButton.None or any other unhandled value should default to Left
        var result = _mapper.Map(MouseButton.None);
        result.Should().Be(MouseButtonCode.Left);
    }

    [Theory]
    [InlineData(MouseButton.ScrollUp)]
    [InlineData(MouseButton.ScrollDown)]
    [InlineData(MouseButton.ScrollLeft)]
    [InlineData(MouseButton.ScrollRight)]
    public void Map_ShouldReturnLeftClick_ForScrollButtons(MouseButton scrollButton)
    {
        // Scroll buttons are not mappable to button codes, should default to Left
        var result = _mapper.Map(scrollButton);
        result.Should().Be(MouseButtonCode.Left);
    }
}
