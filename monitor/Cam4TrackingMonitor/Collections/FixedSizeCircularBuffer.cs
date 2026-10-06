namespace Cam4TrackingMonitor.Collections;

public sealed class FixedSizeCircularBuffer<T>
{
    private readonly T[] _items;
    private int _start;
    public int Count { get; private set; }
    public int Capacity => _items.Length;

    public FixedSizeCircularBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity];
    }

    public void Add(T item)
    {
        if (Count < Capacity)
        {
            _items[(_start + Count) % Capacity] = item;
            Count++;
            return;
        }
        _items[_start] = item;
        _start = (_start + 1) % Capacity;
    }

    public IReadOnlyList<T> Snapshot()
    {
        var result = new T[Count];
        for (var index = 0; index < Count; index++) result[index] = _items[(_start + index) % Capacity];
        return result;
    }
}
