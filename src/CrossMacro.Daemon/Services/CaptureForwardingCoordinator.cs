using System;
using System.Collections.Generic;
using System.IO;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Linux.Native.UInput;

namespace CrossMacro.Daemon.Services;

internal sealed class CaptureForwardingCoordinator
{
    private readonly int _maxBufferedCaptureEvents;
    private readonly object _sync = new();
    private readonly CaptureForwardingState _state = new();

    public CaptureForwardingCoordinator(int maxBufferedCaptureEvents)
    {
        _maxBufferedCaptureEvents = maxBufferedCaptureEvents;
    }

    public int BeginPendingGeneration()
    {
        lock (_sync)
        {
            var generation = ++_state.NextGeneration;
            _state.PendingGeneration = generation;
            ResetPendingBuffer(_state);
            return generation;
        }
    }

    public Action<UInputNative.input_event> CreateEventForwarder(int generation, DaemonProtocolSession session)
    {
        return inputEvent => ForwardEvent(inputEvent, generation, session);
    }

    public CaptureActivation ActivateGeneration(int requestGeneration)
    {
        Queue<UInputNative.input_event>? bufferedEvents = null;
        int droppedPendingCaptureEvents;

        lock (_sync)
        {
            droppedPendingCaptureEvents = _state.DroppedPendingCaptureEvents;

            if (_state.BufferedCaptureEvents.Count > 0)
            {
                bufferedEvents = new Queue<UInputNative.input_event>(_state.BufferedCaptureEvents);
                _state.BufferedCaptureEvents.Clear();
            }

            _state.ActiveGeneration = requestGeneration;
            _state.PendingGeneration = 0;
            _state.CaptureForwardingEnabled = true;
            _state.DroppedPendingCaptureEvents = 0;
        }

        return new CaptureActivation(droppedPendingCaptureEvents, bufferedEvents);
    }

    public void ResetAfterFailedStart(int requestGeneration)
    {
        lock (_sync)
        {
            if (_state.PendingGeneration == requestGeneration)
            {
                _state.PendingGeneration = 0;
            }

            _state.ActiveGeneration = 0;
            _state.CaptureForwardingEnabled = false;
            ResetPendingBuffer(_state);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _state.PendingGeneration = 0;
            _state.ActiveGeneration = 0;
            _state.CaptureForwardingEnabled = false;
            ResetPendingBuffer(_state);
        }
    }

    private void ForwardEvent(UInputNative.input_event inputEvent, int generation, DaemonProtocolSession session)
    {
        if (session.Disconnected)
        {
            return;
        }

        try
        {
            var shouldWriteEvent = false;

            lock (_sync)
            {
                if (generation == _state.PendingGeneration)
                {
                    if (_state.BufferedCaptureEvents.Count >= _maxBufferedCaptureEvents)
                    {
                        _state.BufferedCaptureEvents.Dequeue();
                        _state.DroppedPendingCaptureEvents++;
                    }

                    _state.BufferedCaptureEvents.Enqueue(inputEvent);
                    return;
                }

                if (_state.CaptureForwardingEnabled && generation == _state.ActiveGeneration)
                {
                    shouldWriteEvent = true;
                }
            }

            if (!shouldWriteEvent)
            {
                return;
            }

            using (session.WriterGate.Enter())
            {
                if (session.Disconnected)
                {
                    return;
                }

                lock (_sync)
                {
                    if (!_state.CaptureForwardingEnabled || generation != _state.ActiveGeneration)
                    {
                        return;
                    }
                }

                session.WriteInputEvent(inputEvent);
                session.Stream.Flush();
            }
        }
        catch (IOException)
        {
            session.MarkDisconnected();
            Log.Debug("[SessionHandler] Stream closed, stopping event forwarding");
        }
        catch (Exception ex)
        {
            session.MarkDisconnected();
            Log.Debug(ex, "[SessionHandler] Failed to write input event");
        }
    }

    private static void ResetPendingBuffer(CaptureForwardingState captureState)
    {
        captureState.BufferedCaptureEvents.Clear();
        captureState.DroppedPendingCaptureEvents = 0;
    }

    public readonly record struct CaptureActivation(
        int DroppedPendingCaptureEvents,
        Queue<UInputNative.input_event>? BufferedEvents)
    {
        public bool HasBufferedEvents => BufferedEvents is { Count: > 0 };
    }

    private sealed class CaptureForwardingState
    {
        public int NextGeneration { get; set; }
        public int PendingGeneration { get; set; }
        public int ActiveGeneration { get; set; }
        public bool CaptureForwardingEnabled { get; set; }
        public int DroppedPendingCaptureEvents { get; set; }
        public Queue<UInputNative.input_event> BufferedCaptureEvents { get; } = new();
    }
}
