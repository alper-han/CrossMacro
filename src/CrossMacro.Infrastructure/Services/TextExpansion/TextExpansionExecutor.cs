using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.TextExpansion;
using Serilog;
using TextExpansionModel = CrossMacro.Core.Models.TextExpansion;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

public sealed class TextExpansionExecutor : ITextExpansionExecutor, IDisposable
{
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly TextExpansionKeyDispatcher _keyDispatcher;
    private readonly TextExpansionClipboardInserter _clipboardInserter;
    private readonly TextExpansionDirectTypingInserter _directTypingInserter;
    private readonly Lock _simulatorLock = new();

    private IInputSimulator? _inputSimulator;
    private bool _isDisposed;

    public TextExpansionExecutor(
        IClipboardService clipboardService,
        IKeyboardLayoutService layoutService,
        Func<IInputSimulator> inputSimulatorFactory)
    {
        ArgumentNullException.ThrowIfNull(clipboardService);
        ArgumentNullException.ThrowIfNull(layoutService);
        ArgumentNullException.ThrowIfNull(inputSimulatorFactory);

        _inputSimulatorFactory = inputSimulatorFactory;
        _keyDispatcher = new TextExpansionKeyDispatcher();
        _clipboardInserter = new TextExpansionClipboardInserter(clipboardService, _keyDispatcher);
        _directTypingInserter = new TextExpansionDirectTypingInserter(layoutService, _keyDispatcher);
    }

    public async Task ExpandAsync(TextExpansionModel expansion)
    {
        ArgumentNullException.ThrowIfNull(expansion);

        try
        {
            var inputSimulator = GetOrCreateInputSimulator();
            var directTypingValidated = false;

            if (ShouldPreValidateDirectTyping(expansion))
            {
                _directTypingInserter.ValidateSupport(inputSimulator, expansion.Replacement);
                directTypingValidated = true;
            }

            await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length);

            if (expansion.InsertionMode == TextInsertionMode.DirectTyping)
            {
                Log.Debug("Inserting expansion using direct typing mode");
                await _directTypingInserter.InsertAsync(inputSimulator, expansion.Replacement);
                return;
            }

            var clipboardSuccess = await _clipboardInserter.TryInsertAsync(
                inputSimulator,
                expansion.Replacement,
                expansion.Method);

            if (clipboardSuccess)
            {
                return;
            }

            if (!directTypingValidated)
            {
                _directTypingInserter.ValidateSupport(inputSimulator, expansion.Replacement);
            }

            await _directTypingInserter.InsertAsync(inputSimulator, expansion.Replacement);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing expansion");
        }
    }

    public void Dispose()
    {
        IInputSimulator? inputSimulatorToDispose = null;

        lock (_simulatorLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            inputSimulatorToDispose = _inputSimulator;
            _inputSimulator = null;
        }

        inputSimulatorToDispose?.Dispose();
    }

    private bool ShouldPreValidateDirectTyping(TextExpansionModel expansion)
    {
        return expansion.InsertionMode == TextInsertionMode.DirectTyping || !_clipboardInserter.IsSupported;
    }

    private IInputSimulator GetOrCreateInputSimulator()
    {
        lock (_simulatorLock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TextExpansionExecutor));
            }

            if (_inputSimulator is not null)
            {
                return _inputSimulator;
            }

            _inputSimulator = _inputSimulatorFactory();
            _inputSimulator.Initialize(0, 0);
            return _inputSimulator;
        }
    }

    private async Task BackspaceTriggerAsync(IInputSimulator inputSimulator, int triggerLength)
    {
        Log.Debug("Backspacing {Length} chars", triggerLength);
        for (var i = 0; i < triggerLength; i++)
        {
            await _keyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_BACKSPACE);
        }
    }
}
