namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Unique identifier for a <see cref="RenderView"/> managed by <see cref="ViewManager"/>.
/// </summary>
public readonly struct ViewId : IEquatable<ViewId>
{
    private static int _next = 1;

    /// <summary>Numeric value of this identifier.</summary>
    public int Value { get; }

    private ViewId(int value) => Value = value;

    /// <summary>Creates a new unique ViewId. Thread-safe via Interlocked.</summary>
    internal static ViewId Next() => new(Interlocked.Increment(ref _next));

    /// <summary>Sentinel "no view" value.</summary>
    public static ViewId Empty => new(0);

    /// <summary>Returns true when this is the empty/invalid sentinel.</summary>
    public bool IsEmpty => Value == 0;

    public bool Equals(ViewId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ViewId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(ViewId left, ViewId right) => left.Equals(right);
    public static bool operator !=(ViewId left, ViewId right) => !left.Equals(right);
    public override string ToString() => Value == 0 ? "ViewId.Empty" : $"ViewId({Value})";
}
