namespace CasaEngine.Core.Collections;

public sealed class DensePool<T> where T : new()
{
    private static int NextOwnerId;

    private readonly int _ownerId;
    private Slot[] _slots;
    private int[] _denseToSlot;
    private int[] _freeSlots;
    private T[] _elements;
    private int _freeCount;

    public T[] Elements => _elements;

    public int Count { get; private set; }

    public int Capacity => _elements.Length;

    public Span<T> ActiveSpan => _elements.AsSpan(0, Count);

    public DensePool(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "DensePool: capacity must be greater than zero.");
        }

        _ownerId = Interlocked.Increment(ref NextOwnerId);
        _elements = new T[capacity];
        _slots = new Slot[capacity];
        _denseToSlot = new int[capacity];
        _freeSlots = new int[capacity];
        InitializeSlots(0, capacity);
    }

    public Handle Fetch()
    {
        if (_freeCount == 0)
        {
            EnsureCapacity(Capacity + System.Math.Max(4, Capacity / 2));
        }

        int slotIndex = _freeSlots[--_freeCount];
        int denseIndex = Count++;
        Slot slot = _slots[slotIndex];
        slot.DenseIndex = denseIndex;
        slot.Active = true;
        _slots[slotIndex] = slot;
        _denseToSlot[denseIndex] = slotIndex;

        return new Handle(_ownerId, slotIndex, slot.Generation);
    }

    public T this[Handle handle] => _elements[GetIndex(handle)];

    public ref T GetReference(Handle handle)
    {
        return ref _elements[GetIndex(handle)];
    }

    public bool TryGet(Handle handle, out T element)
    {
        if (TryGetIndex(handle, out int denseIndex))
        {
            element = _elements[denseIndex];
            return true;
        }

        element = default;
        return false;
    }

    public int GetIndex(Handle handle)
    {
        if (TryGetIndex(handle, out int denseIndex))
        {
            return denseIndex;
        }

        throw new InvalidOperationException("DensePool: handle is not active in this pool.");
    }

    public bool TryGetIndex(Handle handle, out int denseIndex)
    {
        if (!IsActive(handle))
        {
            denseIndex = -1;
            return false;
        }

        denseIndex = _slots[handle.SlotIndex].DenseIndex;
        return true;
    }

    public void Release(Handle handle)
    {
        if (!TryRelease(handle))
        {
            throw new InvalidOperationException("DensePool: handle is not active in this pool.");
        }
    }

    public bool TryRelease(Handle handle)
    {
        if (!IsActive(handle))
        {
            return false;
        }

        int releasedSlotIndex = handle.SlotIndex;
        int releasedDenseIndex = _slots[releasedSlotIndex].DenseIndex;
        int lastDenseIndex = Count - 1;

        if (releasedDenseIndex != lastDenseIndex)
        {
            SwapDenseElements(releasedDenseIndex, lastDenseIndex);
        }

        Count--;
        _denseToSlot[Count] = releasedSlotIndex;

        Slot releasedSlot = _slots[releasedSlotIndex];
        releasedSlot.Active = false;
        releasedSlot.DenseIndex = -1;
        releasedSlot.Generation = NextGeneration(releasedSlot.Generation);
        _slots[releasedSlotIndex] = releasedSlot;
        _freeSlots[_freeCount++] = releasedSlotIndex;
        return true;
    }

    public void Swap(int firstIndex, int secondIndex)
    {
        ValidateDenseIndex(firstIndex);
        ValidateDenseIndex(secondIndex);

        if (firstIndex == secondIndex)
        {
            return;
        }

        SwapDenseElements(firstIndex, secondIndex);
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= Capacity)
        {
            return;
        }

        int oldCapacity = Capacity;
        Array.Resize(ref _slots, capacity);
        Array.Resize(ref _denseToSlot, capacity);
        Array.Resize(ref _freeSlots, capacity);
        Array.Resize(ref _elements, capacity);
        InitializeSlots(oldCapacity, capacity);
    }

    private void InitializeSlots(int startIndex, int endIndex)
    {
        for (int index = startIndex; index < endIndex; index++)
        {
            _elements[index] = new T();
            _slots[index] = new Slot
            {
                DenseIndex = -1,
                Generation = 1,
            };
        }

        for (int index = endIndex - 1; index >= startIndex; index--)
        {
            _freeSlots[_freeCount++] = index;
        }
    }

    private bool IsActive(Handle handle)
    {
        if (handle.OwnerId != _ownerId || handle.SlotIndex < 0 || handle.SlotIndex >= _slots.Length)
        {
            return false;
        }

        Slot slot = _slots[handle.SlotIndex];
        return slot.Active && slot.Generation == handle.Generation;
    }

    private void SwapDenseElements(int firstIndex, int secondIndex)
    {
        (_elements[firstIndex], _elements[secondIndex]) = (_elements[secondIndex], _elements[firstIndex]);

        int firstSlotIndex = _denseToSlot[firstIndex];
        int secondSlotIndex = _denseToSlot[secondIndex];
        _denseToSlot[firstIndex] = secondSlotIndex;
        _denseToSlot[secondIndex] = firstSlotIndex;

        Slot firstSlot = _slots[firstSlotIndex];
        Slot secondSlot = _slots[secondSlotIndex];
        firstSlot.DenseIndex = secondIndex;
        secondSlot.DenseIndex = firstIndex;
        _slots[firstSlotIndex] = firstSlot;
        _slots[secondSlotIndex] = secondSlot;
    }

    private void ValidateDenseIndex(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "DensePool: index is outside the active dense range.");
        }
    }

    private static int NextGeneration(int generation)
    {
        return generation == int.MaxValue ? 1 : generation + 1;
    }

    private struct Slot
    {
        public int DenseIndex;
        public int Generation;
        public bool Active;
    }

    public readonly struct Handle : IEquatable<Handle>
    {
        internal int OwnerId { get; }

        internal int SlotIndex { get; }

        internal int Generation { get; }

        internal Handle(int ownerId, int slotIndex, int generation)
        {
            OwnerId = ownerId;
            SlotIndex = slotIndex;
            Generation = generation;
        }

        public bool Equals(Handle other)
        {
            return OwnerId == other.OwnerId && SlotIndex == other.SlotIndex && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is Handle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OwnerId, SlotIndex, Generation);
        }

        public static bool operator ==(Handle left, Handle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Handle left, Handle right)
        {
            return !left.Equals(right);
        }
    }
}