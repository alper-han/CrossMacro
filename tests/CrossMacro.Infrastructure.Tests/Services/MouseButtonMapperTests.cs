
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class MouseButtonMapperTests
{
    private readonly MouseButtonMapper _mapper;

    public MouseButtonMapperTests()
    {
        _mapper = new MouseButtonMapper();
    }

    #region GetMouseButtonName Tests

    [Theory]
    [InlineData(272, "Mouse Left")]
    [InlineData(273, "Mouse Right")]
    [InlineData(274, "Mouse Middle")]
    [InlineData(275, "Mouse Side")]
    [InlineData(276, "Mouse Extra")]
    [InlineData(277, "Mouse Forward")]
    [InlineData(278, "Mouse Back")]
    [InlineData(279, "Mouse Task")]
    public void GetMouseButtonName_ShouldReturnCorrectName(int buttonCode, string expectedName)
    {
        var result = _mapper.GetMouseButtonName(buttonCode);
        _ = result.Should().Be(expectedName);
    }

    [Fact]
    public void GetMouseButtonName_ShouldReturnGenericName_ForUnknownButton()
    {
        // Arrange - button code in valid range but not mapped
        const int unknownButton = 280;

        // Act
        var result = _mapper.GetMouseButtonName(unknownButton);

        // Assert - should return generic "Mouse9" (280 - 272 + 1 = 9)
        _ = result.Should().Be("Mouse9");
    }

    [Fact]
    public void GetMouseButtonName_ShouldReturnEmpty_ForOutOfRangeCode()
    {
        // Arrange - button code way out of range
        const int invalidCode = 100;

        // Act
        var result = _mapper.GetMouseButtonName(invalidCode);

        // Assert
        _ = result.Should().BeEmpty();
    }

    #endregion

    #region GetButtonCode Tests

    [Theory]
    [InlineData("Mouse Left", 272)]
    [InlineData("Mouse Right", 273)]
    [InlineData("Mouse Middle", 274)]
    [InlineData("Mouse Side", 275)]
    [InlineData("Mouse Extra", 276)]
    public void GetButtonCode_ShouldReturnCorrectCode(string buttonName, int expectedCode)
    {
        var result = _mapper.GetButtonCode(buttonName);
        _ = result.Should().Be(expectedCode);
    }

    [Fact]
    public void GetButtonCode_ShouldBeCaseInsensitive()
    {
        _ = _mapper.GetButtonCode("mouse left").Should().Be(272);
        _ = _mapper.GetButtonCode("MOUSE LEFT").Should().Be(272);
        _ = _mapper.GetButtonCode("Mouse Left").Should().Be(272);
    }

    [Fact]
    public void GetButtonCode_ShouldReturnMinusOne_ForUnknownName()
    {
        var result = _mapper.GetButtonCode("Unknown Button");
        _ = result.Should().Be(-1);
    }

    #endregion

    #region IsMouseButton Tests

    [Theory]
    [InlineData(272, true)]  // BTN_LEFT
    [InlineData(273, true)]  // BTN_RIGHT
    [InlineData(279, true)]  // BTN_TASK
    [InlineData(285, true)]  // Within range (279 + 10 = 289)
    public void IsMouseButton_ShouldReturnTrue_ForValidCodes(int code, bool expected)
    {
        _ = _mapper.IsMouseButton(code).Should().Be(expected);
    }

    [Theory]
    [InlineData(100)]  // Too low
    [InlineData(271)]  // Just below BTN_LEFT
    [InlineData(300)]  // Too high
    public void IsMouseButton_ShouldReturnFalse_ForInvalidCodes(int code)
    {
        _ = _mapper.IsMouseButton(code).Should().BeFalse();
    }

    #endregion
}
