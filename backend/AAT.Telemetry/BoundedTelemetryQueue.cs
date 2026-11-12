namespace AAT.Telemetry;

/// <summary>
/// Bounded in-memory buffer. When full, the oldest item is dropped and the dropped counter increments.
/// </summary>
public sealed class BoundedTelemetryQueue<T>
{
    private readonly Queue<T> _items = new();
    private readonly object _gate = new();
    private long _dropped;

    public BoundedTelemetryQueue(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    public int Capacity { get; }
    public long DroppedCount => Interlocked.Read(ref _dropped);

    public int Count
    {
        get
        {
            lock (_gate)
                return _items.Count;
        }
    }

    public void Enqueue(T item)
    {
        lock (_gate)
        {
            if (_items.Count >= Capacity)
            {
                _items.Dequeue();
                Interlocked.Increment(ref _dropped);
            }

            _items.Enqueue(item);
        }
    }

    public IReadOnlyList<T> DequeueBatch(int max)
    {
        if (max < 1)
            return [];

        lock (_gate)
        {
            var count = Math.Min(max, _items.Count);
            var batch = new List<T>(count);
            for (var i = 0; i < count; i++)
                batch.Add(_items.Dequeue());
            return batch;
        }
    }

    public void RequeueFront(IReadOnlyList<T> items)
    {
        lock (_gate)
        {
            var combined = new Queue<T>(items.Count + _items.Count);
            foreach (var item in items)
                combined.Enqueue(item);
            while (_items.Count > 0)
                combined.Enqueue(_items.Dequeue());

            while (combined.Count > Capacity)
            {
                combined.Dequeue();
                Interlocked.Increment(ref _dropped);
            }

            while (combined.Count > 0)
                _items.Enqueue(combined.Dequeue());
        }
    }
}
