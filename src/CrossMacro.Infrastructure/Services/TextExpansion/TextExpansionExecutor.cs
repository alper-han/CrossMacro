
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public sealed class TextExpansionExecutor : ITextExpansionExecutor, IDisposable
{
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
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
        _clipboardInserter = new TextExpansionClipboardInserter(clipboardService);
        _directTypingInserter = new TextExpansionDirectTypingInserter(layoutService);
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

            if (expansion.InsertionMode is TextInsertionMode.DirectTyping)
            {
                await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length).ConfigureAwait(false);
                await Task.Delay(TextExpansionExecutionTimings.TriggerBackspaceSettleDelay).ConfigureAwait(false);
                Log.Debug("Inserting expansion using direct typing mode");
                await _directTypingInserter.InsertAsync(
                    inputSimulator,
                    expansion.Replacement,
                    expansion.DirectTypingMethod).ConfigureAwait(false);
                return;
            }

            var preparedPaste = await _clipboardInserter.TryPrepareAsync(expansion.Replacement).ConfigureAwait(false);
            if (preparedPaste is not null)
            {
                try
                {
                    await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length).ConfigureAwait(false);
                    await Task.Delay(TextExpansionExecutionTimings.TriggerBackspaceSettleDelay).ConfigureAwait(false);
                    await TextExpansionClipboardInserter.CommitAsync(inputSimulator, preparedPaste, expansion.Method).ConfigureAwait(false);
                }
                finally
                {
                    await _clipboardInserter.RestoreAsync(preparedPaste).ConfigureAwait(false);
                }

                return;
            }

            Log.Information("Clipboard paste-mode insertion failed; falling back to direct typing");

            if (!directTypingValidated)
            {
                _directTypingInserter.ValidateSupport(inputSimulator, expansion.Replacement);
            }

            await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length).ConfigureAwait(false);
            await Task.Delay(TextExpansionExecutionTimings.TriggerBackspaceSettleDelay).ConfigureAwait(false);
            await _directTypingInserter.InsertAsync(
                inputSimulator,
                expansion.Replacement,
                expansion.DirectTypingMethod).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Error executing expansion");
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
        return expansion.InsertionMode is TextInsertionMode.DirectTyping || !_clipboardInserter.IsSupported;
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

    private static async Task BackspaceTriggerAsync(IInputSimulator inputSimulator, int triggerLength)
    {
        Log.Debug("Backspacing {Length} chars", triggerLength);
        for (var i = 0; i < triggerLength; i++)
        {
            await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_BACKSPACE).ConfigureAwait(false);
        }
    }
}
