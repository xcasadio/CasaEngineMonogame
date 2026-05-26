namespace CasaEngine.Framework.Scripting.Coroutines;

public readonly struct CoroutineHandle : IEquatable<CoroutineHandle>
{
    public static readonly CoroutineHandle Invalid = new(0, -1, 0);

    public CoroutineHandle(int managerId, int slot, int generation)
    {
        ManagerId = managerId;
        Slot = slot;
        Generation = generation;
    }

    public int ManagerId { get; }

    public int Slot { get; }

    public int Generation { get; }

    public bool IsValid => ManagerId != 0 && Slot >= 0 && Generation > 0;

    public bool Equals(CoroutineHandle other)
    {
        return ManagerId == other.ManagerId
            && Slot == other.Slot
            && Generation == other.Generation;
    }

    public override bool Equals(object obj)
    {
        return obj is CoroutineHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ManagerId, Slot, Generation);
    }

    public static bool operator ==(CoroutineHandle left, CoroutineHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CoroutineHandle left, CoroutineHandle right)
    {
        return !left.Equals(right);
    }
}