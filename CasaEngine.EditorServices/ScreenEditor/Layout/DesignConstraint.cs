namespace CasaEngine.EditorServices.ScreenEditor.Layout;

/// <summary>Specifies how a dimension should adapt in a responsive layout.</summary>
public enum ConstraintAxis
{
    /// <summary>Fixed pixel size (maps to an explicit Width/Height value).</summary>
    Fixed,

    /// <summary>Proportional to parent (maps to HorizontalAlignment/VerticalAlignment = Stretch + optional proportional weight).</summary>
    Stretch,

    /// <summary>Size is determined by content (omit Width/Height, let layout measure).</summary>
    Auto,
}

/// <summary>
/// Design-time constraint associated with a <see cref="DocumentModel.UIScreenNode"/>.
/// Describes how the node's width and height should behave when the preview resolution changes.
/// </summary>
public sealed class DesignConstraint
{
    /// <summary>Constraint applied to the width axis.</summary>
    public ConstraintAxis Width { get; set; } = ConstraintAxis.Fixed;

    /// <summary>Constraint applied to the height axis.</summary>
    public ConstraintAxis Height { get; set; } = ConstraintAxis.Fixed;

    /// <summary>
    /// When <see cref="Width"/> is <see cref="ConstraintAxis.Stretch"/>, this is the
    /// proportional weight relative to siblings (analogous to Grid star-sizing).
    /// </summary>
    public float WidthWeight { get; set; } = 1f;

    /// <summary>Same as <see cref="WidthWeight"/> for the height axis.</summary>
    public float HeightWeight { get; set; } = 1f;

    // ─── MGUI property mapping ────────────────────────────────────────────

    /// <summary>Returns the MGUI HorizontalAlignment value that should be applied, or null if no change is needed.</summary>
    public string? ToHorizontalAlignment() => Width switch
    {
        ConstraintAxis.Stretch => "Stretch",
        ConstraintAxis.Auto    => null,
        _                      => null,
    };

    /// <summary>Returns the MGUI VerticalAlignment value that should be applied, or null if no change is needed.</summary>
    public string? ToVerticalAlignment() => Height switch
    {
        ConstraintAxis.Stretch => "Stretch",
        ConstraintAxis.Auto    => null,
        _                      => null,
    };

    /// <summary>Returns the serialized width property value, or null to remove the Width property.</summary>
    public string? ToSerializedWidth() => Width switch
    {
        ConstraintAxis.Fixed   => null,   // caller keeps existing fixed value
        ConstraintAxis.Stretch => null,   // Width removed, HA=Stretch used instead
        ConstraintAxis.Auto    => null,   // Width removed entirely
        _                      => null,
    };
}
