using System;
using System.Collections.Generic;

namespace CrossMacro.Platform.Linux.Ipc;

internal enum CaptureCommandType
{
    None = 0,
    Start = 1,
    Stop = 2
}

internal readonly record struct CaptureCommand(
    CaptureCommandType Type,
    bool CaptureMouse = false,
    bool CaptureKeyboard = false);

internal sealed class CaptureSubscriptionCoordinator
{
    private readonly Dictionary<string, (bool Mouse, bool Keyboard)> _subscriptions = new(StringComparer.Ordinal);

    private bool _transportCaptureActive;
    private bool _transportCaptureMouse;
    private bool _transportCaptureKeyboard;

    public CaptureCommand SetSubscription(string consumerId, bool captureMouse, bool captureKeyboard)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            throw new ArgumentException("Consumer id cannot be null or whitespace.", nameof(consumerId));
        }

        if (captureMouse || captureKeyboard)
        {
            _subscriptions[consumerId] = (captureMouse, captureKeyboard);
        }
        else
        {
            _subscriptions.Remove(consumerId);
        }

        return EvaluateCommand();
    }

    public CaptureCommand RemoveSubscription(string consumerId)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return default;
        }

        _subscriptions.Remove(consumerId);
        return EvaluateCommand();
    }

    public CaptureCommand ResetTransportStateAndGetCommand()
    {
        _transportCaptureActive = false;
        _transportCaptureMouse = false;
        _transportCaptureKeyboard = false;
        return EvaluateCommand();
    }

    public void Clear()
    {
        _subscriptions.Clear();
        _transportCaptureActive = false;
        _transportCaptureMouse = false;
        _transportCaptureKeyboard = false;
    }

    private CaptureCommand EvaluateCommand()
    {
        bool captureMouse = false;
        bool captureKeyboard = false;

        foreach (var request in _subscriptions.Values)
        {
            captureMouse |= request.Mouse;
            captureKeyboard |= request.Keyboard;

            if (captureMouse && captureKeyboard)
            {
                break;
            }
        }

        if (!captureMouse && !captureKeyboard)
        {
            if (_transportCaptureActive)
            {
                _transportCaptureActive = false;
                _transportCaptureMouse = false;
                _transportCaptureKeyboard = false;
                return new CaptureCommand(CaptureCommandType.Stop);
            }

            return default;
        }

        if (!_transportCaptureActive ||
            captureMouse != _transportCaptureMouse ||
            captureKeyboard != _transportCaptureKeyboard)
        {
            _transportCaptureActive = true;
            _transportCaptureMouse = captureMouse;
            _transportCaptureKeyboard = captureKeyboard;

            return new CaptureCommand(CaptureCommandType.Start, captureMouse, captureKeyboard);
        }

        return default;
    }
}
