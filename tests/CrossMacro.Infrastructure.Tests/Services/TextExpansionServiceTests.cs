using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class TextExpansionServiceTests
{
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    private readonly ITextExpansionStorageService _storageService;
    private readonly IKeyboardLayoutService _layoutService;
    private readonly IInputCapture _inputCapture;
    private readonly IInputSimulator _inputSimulator;
    private readonly TextExpansionService _service;

    public TextExpansionServiceTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });

        _clipboardService = Substitute.For<IClipboardService>();
        _storageService = Substitute.For<ITextExpansionStorageService>();
        _layoutService = Substitute.For<IKeyboardLayoutService>();
        
        _inputCapture = Substitute.For<IInputCapture>();
        _inputSimulator = Substitute.For<IInputSimulator>();

        _service = new TextExpansionService(
            _settingsService,
            _clipboardService,
            _storageService,
            _layoutService,
            () => _inputCapture,
            () => _inputSimulator);
    }

    [Fact]
    public async Task Start_WhenEnabled_StartsInputCapture()
    {
        // Act
        _service.Start();

        // Assert
        Assert.True(_service.IsRunning);
        _inputCapture.Received(1).Configure(false, true);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenDisabled_DoesNotStart()
    {
        // Arrange
        _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = false });

        // Act
        _service.Start();

        // Assert
        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Stop_StopsInputCapture()
    {
        // Arrange
        _service.Start();

        // Act
        _service.Stop();

        // Assert
        // We verify Stop and Dispose
        _inputCapture.Received(1).Stop();
        _inputCapture.Received(1).Dispose();
    }
}
