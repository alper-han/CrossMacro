using System;
using System.Collections.Generic;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class HotkeyParserTests
{
    private readonly IKeyCodeMapper _mapper;
    private readonly HotkeyParser _parser;

    public HotkeyParserTests()
    {
        _mapper = Substitute.For<IKeyCodeMapper>();
        _parser = new HotkeyParser(_mapper);
    }

    [Fact]
    public void Parse_WhenStringIsEmpty_ReturnsEmptyMapping()
    {
        // Act
        var result = _parser.Parse("");

        // Assert
        result.MainKey.Should().Be(-1);
        result.RequiredModifiers.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhenSingleKey_ReturnsMappingWithMainKey()
    {
        // Arrange
        _mapper.GetKeyCode("A").Returns(30);
        _mapper.IsModifierKeyCode(30).Returns(false);

        // Act
        var result = _parser.Parse("A");

        // Assert
        result.MainKey.Should().Be(30);
        result.RequiredModifiers.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhenKeyWithModifier_ReturnsMappingWithModifier()
    {
        // Arrange
        _mapper.GetKeyCode("Ctrl").Returns(29);
        _mapper.IsModifierKeyCode(29).Returns(true);
        
        _mapper.GetKeyCode("A").Returns(30);
        _mapper.IsModifierKeyCode(30).Returns(false);

        // Act
        var result = _parser.Parse("Ctrl+A");

        // Assert
        result.MainKey.Should().Be(30);
        result.RequiredModifiers.Should().Contain(29);
    }

    [Fact]
    public void Parse_WhenMultipleModifiers_ReturnsMappingWithAllModifiers()
    {
        // Arrange
        _mapper.GetKeyCode("Ctrl").Returns(29);
        _mapper.IsModifierKeyCode(29).Returns(true);
        
        _mapper.GetKeyCode("Shift").Returns(42);
        _mapper.IsModifierKeyCode(42).Returns(true);
        
        _mapper.GetKeyCode("B").Returns(48);
        _mapper.IsModifierKeyCode(48).Returns(false);

        // Act
        var result = _parser.Parse("Ctrl+Shift+B");

        // Assert
        result.MainKey.Should().Be(48);
        result.RequiredModifiers.Should().BeEquivalentTo([29, 42]);
    }
    
    [Fact]
    public void Parse_IgnoresUnknownKeys()
    {
         // Arrange
        _mapper.GetKeyCode("Unknown").Returns(-1);
        _mapper.GetKeyCode("A").Returns(30);
        _mapper.IsModifierKeyCode(30).Returns(false);

        // Act
        var result = _parser.Parse("Unknown+A");

        // Assert
        result.MainKey.Should().Be(30);
    }
}
