using System;
using System.Threading;

namespace CrossMacro.Daemon.Services;

internal sealed class OrderedWriteGate
{
    private readonly object _sync = new();
    private long _nextTicket;
    private long _nowServing;

    internal long IssuedTicketCount => Volatile.Read(ref _nextTicket);

    public Releaser Enter()
    {
        var ticket = Interlocked.Increment(ref _nextTicket) - 1;

        lock (_sync)
        {
            while (ticket != _nowServing)
            {
                Monitor.Wait(_sync);
            }
        }

        return new Releaser(this);
    }

    private void Exit()
    {
        lock (_sync)
        {
            _nowServing++;
            Monitor.PulseAll(_sync);
        }
    }

    public readonly struct Releaser : IDisposable
    {
        private readonly OrderedWriteGate? _owner;

        internal Releaser(OrderedWriteGate owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner?.Exit();
        }
    }
}
