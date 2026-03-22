using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Layout;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Reversible command that applies a <see cref="DesignConstraint"/> to a node by
/// writing the appropriate HorizontalAlignment / VerticalAlignment properties and
/// removing or keeping explicit Width / Height values.
/// </summary>
public sealed class ApplyConstraintCommand : IUIScreenCommand
{
    private readonly UIScreenNode _node;
    private readonly DesignConstraint _constraint;

    // Snapshots of previous values for undo
    private readonly string? _prevHAlignment;
    private readonly string? _prevVAlignment;
    private readonly string? _prevWidth;
    private readonly string? _prevHeight;

    public string Description => "Apply responsive constraint";

    public ApplyConstraintCommand(UIScreenNode node, DesignConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(constraint);

        _node = node;
        _constraint = constraint;

        _prevHAlignment = node.Properties.TryGetValue("HorizontalAlignment", out var ha) ? ha.SerializedValue : null;
        _prevVAlignment = node.Properties.TryGetValue("VerticalAlignment",   out var va) ? va.SerializedValue : null;
        _prevWidth      = node.Properties.TryGetValue("Width",               out var w)  ? w.SerializedValue  : null;
        _prevHeight     = node.Properties.TryGetValue("Height",              out var h)  ? h.SerializedValue  : null;
    }

    public void Execute()
    {
        var ha = _constraint.ToHorizontalAlignment();
        var va = _constraint.ToVerticalAlignment();

        if (ha != null)
        {
            _node.SetProperty("HorizontalAlignment", ha);
        }

        if (va != null)
        {
            _node.SetProperty("VerticalAlignment", va);
        }

        // For Stretch/Auto axes, remove explicit size so layout drives it
        if (_constraint.Width != ConstraintAxis.Fixed)
        {
            _node.RemoveProperty("Width");
        }

        if (_constraint.Height != ConstraintAxis.Fixed)
        {
            _node.RemoveProperty("Height");
        }
    }

    public void Undo()
    {
        RestoreProperty("HorizontalAlignment", _prevHAlignment);
        RestoreProperty("VerticalAlignment",   _prevVAlignment);
        RestoreProperty("Width",               _prevWidth);
        RestoreProperty("Height",              _prevHeight);
    }

    private void RestoreProperty(string name, string? value)
    {
        if (value != null)
        {
            _node.SetProperty(name, value);
        }
        else
        {
            _node.RemoveProperty(name);
        }
    }
}
