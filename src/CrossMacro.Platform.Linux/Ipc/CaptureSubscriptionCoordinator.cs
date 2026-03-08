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

    public bool HasSubscriptions => _subscriptions.Count > 0;

    public void SetSubscription(string consumerId, bool captureMouse, bool captureKeyboard)
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
    }

    public void RemoveSubscription(string consumerId)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return;
        }

        _subscriptions.Remove(consumerId);
    }

    public bool TryGetSubscription(string consumerId, out bool captureMouse, out bool captureKeyboard)
    {
        captureMouse = false;
        captureKeyboard = false;

        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return false;
        }

        if (!_subscriptions.TryGetValue(consumerId, out var subscription))
        {
            return false;
        }

        captureMouse = subscription.Mouse;
        captureKeyboard = subscription.Keyboard;
        return true;
    }

    public void ResetTransportState()
    {
        _transportCaptureActive = false;
        _transportCaptureMouse = false;
        _transportCaptureKeyboard = false;
    }

    public void Clear()
    {
        _subscriptions.Clear();
        ResetTransportState();
    }

    public void MarkCommandIssued(CaptureCommand command)
    {
        switch (command.Type)
        {
            case CaptureCommandType.Start:
                _transportCaptureActive = true;
                _transportCaptureMouse = command.CaptureMouse;
                _transportCaptureKeyboard = command.CaptureKeyboard;
                break;
            case CaptureCommandType.Stop:
                ResetTransportState();
                break;
        }
    }

    public void MarkTransportStopped()
    {
        ResetTransportState();
    }

    public CaptureCommand GetRequiredCommand()
    {
        return EvaluateCommand();
    }

    public CaptureCommand GetTransportCommand()
    {
        if (!_transportCaptureActive)
        {
            return default;
        }

        return new CaptureCommand(CaptureCommandType.Start, _transportCaptureMouse, _transportCaptureKeyboard);
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
                return new CaptureCommand(CaptureCommandType.Stop);
            }

            return default;
        }

        if (!_transportCaptureActive ||
            captureMouse != _transportCaptureMouse ||
            captureKeyboard != _transportCaptureKeyboard)
        {
            return new CaptureCommand(CaptureCommandType.Start, captureMouse, captureKeyboard);
        }

        return default;
    }
}
