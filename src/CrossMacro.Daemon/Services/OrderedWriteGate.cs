
namespace CrossMacro.Daemon.Services;

internal sealed class OrderedWriteGate
{
    internal enum WaiterState
    {
        Queued,
        Granted,
        Finished,
        Acquired,
    }

    private readonly Lock _sync = new();
    private readonly LinkedList<Waiter> _waiters = new();
    private bool _held;
    private long _nextTicket;

    internal long IssuedTicketCount => Volatile.Read(ref _nextTicket);

    internal Action<long>? TicketIssued { get; set; }

    internal Action<Waiter>? BeforeAcquire { get; set; }

    public async ValueTask<Releaser> EnterAsync(CancellationToken cancellationToken = default)
    {
        var ticket = Interlocked.Increment(ref _nextTicket) - 1;
        TicketIssued?.Invoke(ticket);
        var waiter = new Waiter();

        lock (_sync)
        {
            waiter.Node = _waiters.AddLast(waiter);
            if (waiter.Node == _waiters.First)
            {
                GrantNextWaiter();
            }
        }

        try
        {
            await waiter.Granted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            BeforeAcquire?.Invoke(waiter);
            lock (_sync)
            {
                if (waiter.State is not WaiterState.Granted || cancellationToken.IsCancellationRequested)
                {
                    Cancel(waiter);
                    throw new OperationCanceledException(cancellationToken);
                }

                waiter.State = WaiterState.Acquired;
            }

            return new Releaser(this, waiter);
        }
        catch
        {
            Cancel(waiter);
            throw;
        }
    }

    private void Cancel(Waiter waiter)
    {
        lock (_sync)
        {
            if (waiter.State is not (WaiterState.Queued or WaiterState.Granted))
            {
                return;
            }

            if (waiter.State is WaiterState.Granted)
            {
                _held = false;
            }

            waiter.State = WaiterState.Finished;
            if (waiter.Node is { } node)
            {
                _waiters.Remove(node);
            }
            GrantNextWaiter();
        }
    }

    private void Exit(Waiter waiter)
    {
        lock (_sync)
        {
            if (waiter.State is not WaiterState.Acquired)
            {
                return;
            }

            waiter.State = WaiterState.Finished;
            _held = false;
            if (waiter.Node is { } node)
            {
                _waiters.Remove(node);
            }
            GrantNextWaiter();
        }
    }

    private void GrantNextWaiter()
    {
        if (_held)
        {
            return;
        }

        while (_waiters.First is { } first)
        {
            var waiter = first.Value;
            if (waiter.State is WaiterState.Finished)
            {
                _waiters.RemoveFirst();
                continue;
            }

            waiter.State = WaiterState.Granted;
            _held = true;
            waiter.Granted.SetResult();
            return;
        }
    }

    internal sealed class Waiter
    {
        public TaskCompletionSource Granted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
        public WaiterState State { get; set; }
    }

    internal readonly struct Releaser : IDisposable
    {
        private readonly OrderedWriteGate? _owner;
        private readonly Waiter? _waiter;

        internal Releaser(OrderedWriteGate owner, Waiter waiter)
        {
            _owner = owner;
            _waiter = waiter;
        }

        public void Dispose()
        {
            if (_owner is not null && _waiter is not null)
            {
                _owner.Exit(_waiter);
            }
        }
    }
}
