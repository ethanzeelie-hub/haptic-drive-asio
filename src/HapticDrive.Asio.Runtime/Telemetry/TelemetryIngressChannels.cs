using System.Collections.Generic;
using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Runtime.Telemetry;

internal sealed class TelemetryIngressDropOldestQueue
{
    private readonly object _gate = new();
    private readonly Queue<UdpTelemetryPacket> _items = new();
    private readonly SemaphoreSlim _itemsAvailable = new(0);
    private readonly int _capacity;
    private bool _isCompleted;
    private long _droppedItemCount;

    public TelemetryIngressDropOldestQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Queue capacity must be positive.");
        }

        _capacity = capacity;
    }

    public int Capacity => _capacity;

    public long DroppedItemCount => Interlocked.Read(ref _droppedItemCount);

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public bool TryEnqueue(UdpTelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var shouldRelease = false;
        lock (_gate)
        {
            if (_isCompleted)
            {
                return false;
            }

            if (_items.Count == _capacity)
            {
                _items.Dequeue();
                Interlocked.Increment(ref _droppedItemCount);
            }
            else
            {
                shouldRelease = true;
            }

            _items.Enqueue(packet);
        }

        if (shouldRelease)
        {
            _itemsAvailable.Release();
        }

        return true;
    }

    public void Complete()
    {
        lock (_gate)
        {
            _isCompleted = true;
        }

        _itemsAvailable.Release();
    }

    public async ValueTask<UdpTelemetryPacket?> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await _itemsAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                if (_items.Count > 0)
                {
                    return _items.Dequeue();
                }

                if (_isCompleted)
                {
                    return null;
                }
            }
        }
    }
}
