
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public sealed class TextExpansionExecutor : ITextExpansionExecutor, IDisposable, IAsyncDisposable
{
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly TextExpansionClipboardInserter _clipboardInserter;
    private readonly TextExpansionDirectTypingInserter _directTypingInserter;
    private readonly Lock _simulatorLock = new();
    private readonly SemaphoreSlim _simulatorLease = new(1, 1);

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

    public async Task ExpandAsync(TextExpansionModel expansion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expansion);

        await _simulatorLease.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_simulatorLock)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
            }

            var inputSimulator = await GetOrCreateInputSimulatorAsync(cancellationToken).ConfigureAwait(false);
            var directTypingValidated = false;

            if (ShouldPreValidateDirectTyping(expansion))
            {
                _directTypingInserter.ValidateSupport(inputSimulator, expansion.Replacement);
                directTypingValidated = true;
            }

            if (expansion.InsertionMode is TextInsertionMode.DirectTyping)
            {
                await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length, cancellationToken).ConfigureAwait(false);
                await Task.Delay(TextExpansionExecutionTimings.TriggerBackspaceSettleDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                Log.Debug("Inserting expansion using direct typing mode");
                await _directTypingInserter.InsertAsync(
                    inputSimulator,
                    expansion.Replacement,
                    expansion.DirectTypingMethod,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var preparedPaste = await _clipboardInserter.TryPrepareAsync(expansion.Replacement).ConfigureAwait(false);
            if (preparedPaste is not null)
            {
                try
                {
                    await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(TextExpansionExecutionTimings.TriggerBackspaceSettleDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                    await TextExpansionClipboardInserter.CommitAsync(inputSimulator, preparedPaste, expansion.Method, cancellationToken).ConfigureAwait(false);
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

            await BackspaceTriggerAsync(inputSimulator, expansion.Trigger.Length, cancellationToken).ConfigureAwait(false);
            await Task.Delay(TextExpansionExecutionTimings.TriggerBackspaceSettleDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
            await _directTypingInserter.InsertAsync(
                inputSimulator,
                expansion.Replacement,
                expansion.DirectTypingMethod,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error executing expansion");
        }
        finally
        {
            _ = _simulatorLease.Release();
        }
    }

    public void Dispose()
    {
        IInputSimulator? inputSimulatorToDispose;

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

        _simulatorLease.Wait();
        try
        {
            inputSimulatorToDispose?.Dispose();
        }
        finally
        {
            _ = _simulatorLease.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        IInputSimulator? inputSimulatorToDispose;

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

        await _simulatorLease.WaitAsync().ConfigureAwait(false);
        try
        {
            inputSimulatorToDispose?.Dispose();
        }
        finally
        {
            _ = _simulatorLease.Release();
        }
    }

    private bool ShouldPreValidateDirectTyping(TextExpansionModel expansion)
    {
        return expansion.InsertionMode is TextInsertionMode.DirectTyping || !_clipboardInserter.IsSupported;
    }

    private async Task<IInputSimulator> GetOrCreateInputSimulatorAsync(CancellationToken cancellationToken)
    {
        IInputSimulator simulator;
        lock (_simulatorLock)
        {
            if (_isDisposed)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
            }

            if (_inputSimulator is not null)
            {
                return _inputSimulator;
            }

            simulator = _inputSimulatorFactory();
        }

        await simulator.InitializeAsync(0, 0, cancellationToken).ConfigureAwait(false);

        lock (_simulatorLock)
        {
            if (_isDisposed)
            {
                simulator.Dispose();
                throw new ObjectDisposedException(nameof(TextExpansionExecutor));
            }

            if (_inputSimulator is null)
            {
                _inputSimulator = simulator;
                return simulator;
            }

            simulator.Dispose();
            return _inputSimulator;
        }
    }

    private static async Task BackspaceTriggerAsync(IInputSimulator inputSimulator, int triggerLength, CancellationToken cancellationToken)
    {
        Log.Debug("Backspacing {Length} chars", triggerLength);
        for (var i = 0; i < triggerLength; i++)
        {
            await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_BACKSPACE, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
