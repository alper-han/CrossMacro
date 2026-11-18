
namespace CrossMacro.Platform.Linux.Native.Evdev;

/// <summary>
/// Forwards evdev events to a consumer while keeping the consumer's view of key state coherent across
/// <c>SYN_DROPPED</c>. After a drop, events are discarded until the next <c>SYN_REPORT</c>; the device
/// key state is then re-read and the difference to what was last forwarded is emitted as press/release events.
/// </summary>
internal sealed class EvdevEventSynchronizer(
    string deviceName,
    Func<byte[], bool> readKeySnapshot,
    Action<UInputNative.input_event> emit)
{
    // EVIOCGKEY returns one bit per key code below KEY_CNT (0x300).
    internal const int KeyBitmapBytes = 96;

    private readonly byte[] _forwardedKeys = new byte[KeyBitmapBytes];
    private readonly byte[] _snapshot = new byte[KeyBitmapBytes];
    private bool _awaitingReport;

    /// <summary>Records keys that are already down so a later resync does not report them as new presses.</summary>
    public void SeedKeyState()
    {
        if (!readKeySnapshot(_forwardedKeys))
        {
            Array.Clear(_forwardedKeys);
        }
    }

    public void Process(UInputNative.input_event inputEvent)
    {
        if (EvdevReader.IsSynchronizationLost(inputEvent))
        {
            _awaitingReport = true;
            Log.Warning("[{Device}] SYN_DROPPED: Events lost, waiting for SYN_REPORT to resync", deviceName);
            return;
        }

        if (_awaitingReport)
        {
            if (inputEvent.type is UInputNative.EV_SYN && inputEvent.code is UInputNative.SYN_REPORT)
            {
                _awaitingReport = false;
                ResyncKeys();
                emit(inputEvent);
            }

            return;
        }

        if (inputEvent.type is UInputNative.EV_KEY && inputEvent.code < KeyBitmapBytes * 8 && inputEvent.value is 0 or 1)
        {
            SetForwarded(inputEvent.code, inputEvent.value is 1);
        }

        emit(inputEvent);
    }

    private void ResyncKeys()
    {
        if (!readKeySnapshot(_snapshot))
        {
            Log.Warning("[{Device}] Failed to read key state during resync", deviceName);
            return;
        }

        for (var byteIndex = 0; byteIndex < KeyBitmapBytes; byteIndex++)
        {
            var changed = (byte)(_forwardedKeys[byteIndex] ^ _snapshot[byteIndex]);
            if (changed is 0)
            {
                continue;
            }

            _forwardedKeys[byteIndex] = _snapshot[byteIndex];
            for (var bit = 0; bit < 8; bit++)
            {
                if ((changed & (1 << bit)) is 0)
                {
                    continue;
                }

                emit(new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)((byteIndex * 8) + bit),
                    value = (_snapshot[byteIndex] & (1 << bit)) is not 0 ? 1 : 0,
                });
            }
        }

        Log.Debug("[{Device}] Resync completed after SYN_DROPPED", deviceName);
    }

    private void SetForwarded(ushort code, bool pressed)
    {
        var mask = (byte)(1 << (code % 8));
        if (pressed)
        {
            _forwardedKeys[code / 8] |= mask;
        }
        else
        {
            _forwardedKeys[code / 8] &= (byte)~mask;
        }
    }
}
