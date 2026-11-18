
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class MouseButtonMapperTests
{
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
        var result = MouseButtonMapper.GetMouseButtonName(buttonCode);
        _ = result.Should().Be(expectedName);
    }

    [Fact]
    public void GetMouseButtonName_ShouldReturnGenericName_ForUnknownButton()
    {
        // Arrange - button code in valid range but not mapped
        const int unknownButton = 280;

        // Act
        var result = MouseButtonMapper.GetMouseButtonName(unknownButton);

        // Assert - should return generic "Mouse9" (280 - 272 + 1 = 9)
        _ = result.Should().Be("Mouse9");
    }

    [Fact]
    public void GetMouseButtonName_ShouldReturnEmpty_ForOutOfRangeCode()
    {
        // Arrange - button code way out of range
        const int invalidCode = 100;

        // Act
        var result = MouseButtonMapper.GetMouseButtonName(invalidCode);

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
        var result = MouseButtonMapper.GetButtonCode(buttonName);
        _ = result.Should().Be(expectedCode);
    }

    [Fact]
    public void GetButtonCode_ShouldBeCaseInsensitive()
    {
        _ = MouseButtonMapper.GetButtonCode("mouse left").Should().Be(272);
        _ = MouseButtonMapper.GetButtonCode("MOUSE LEFT").Should().Be(272);
        _ = MouseButtonMapper.GetButtonCode("Mouse Left").Should().Be(272);
    }

    [Fact]
    public void GetButtonCode_ShouldReturnMinusOne_ForUnknownName()
    {
        var result = MouseButtonMapper.GetButtonCode("Unknown Button");
        _ = result.Should().Be(-1);
    }

    #endregion

    #region Round trip

    [Theory]
    [InlineData(272)]
    [InlineData(279)]
    [InlineData(280)]
    [InlineData(289)]
    public void GetButtonCode_ShouldParseBackEveryNameProducedByGetMouseButtonName(int code)
    {
        _ = MouseButtonMapper.GetButtonCode(MouseButtonMapper.GetMouseButtonName(code)).Should().Be(code);
    }

    [Theory]
    [InlineData("Mouse9", 280)]
    [InlineData("mouse18", 289)]
    [InlineData("Mouse0", -1)]
    [InlineData("Mouse19", -1)]
    [InlineData("Mouse-1", -1)]
    [InlineData("MouseX", -1)]
    public void GetButtonCode_ShouldParseGenericNamesWithinTheSupportedRange(string name, int expected)
    {
        _ = MouseButtonMapper.GetButtonCode(name).Should().Be(expected);
    }

    #endregion
}
