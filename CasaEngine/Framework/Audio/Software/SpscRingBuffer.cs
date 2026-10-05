namespace CasaEngine.Framework.Audio.Software;

/// <summary>
/// Fixed capacity lock-free ring for exactly one producer thread and exactly one consumer thread.
/// The element is written before the tail index is published (release) and the consumer reads the
/// tail with acquire semantics, so a dequeued element is always fully written. Nothing allocates
/// after construction. The capacity is rounded up to a power of two.
/// </summary>
internal sealed class SpscRingBuffer<T>
{
    private readonly T[] _items;
    private readonly int _mask;

    /// <summary>Next index to read; written by the consumer only.</summary>
    private int _head;

    /// <summary>Next index to write; written by the producer only.</summary>
    private int _tail;

    public SpscRingBuffer(int minimumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumCapacity, 1 << 24);

        var capacity = 1;
        while (capacity < minimumCapacity)
        {
            capacity <<= 1;
        }

        _items = new T[capacity];
        _mask = capacity - 1;
    }

    public int Capacity => _items.Length;

    /// <summary>Producer side: number of elements that can still be enqueued (a lower bound).</summary>
    public int FreeCount => _items.Length - (Volatile.Read(ref _tail) - Volatile.Read(ref _head));

    /// <summary>Producer side. Returns false, without writing, when the ring is full.</summary>
    public bool TryEnqueue(in T item)
    {
        var tail = _tail;
        var head = Volatile.Read(ref _head);

        if (unchecked(tail - head) >= _items.Length)
        {
            return false;
        }

        _items[tail & _mask] = item;
        Volatile.Write(ref _tail, unchecked(tail + 1));
        return true;
    }

    /// <summary>Consumer side. Returns false when the ring is empty.</summary>
    public bool TryDequeue(out T item)
    {
        var head = _head;
        var tail = Volatile.Read(ref _tail);

        if (head == tail)
        {
            item = default;
            return false;
        }

        var index = head & _mask;
        item = _items[index];
        _items[index] = default;
        Volatile.Write(ref _head, unchecked(head + 1));
        return true;
    }
}
