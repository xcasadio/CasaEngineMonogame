using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Reversible command that moves a child node to a new position within its parent's children list.
/// Used when drag-to-move is applied inside layout-driven containers (StackPanel, DockPanel, Grid).
/// </summary>
public sealed class MoveChildCommand : IUIScreenCommand
{
    private readonly UIScreenNode _node;
    private readonly int _oldIndex;
    private readonly int _newIndex;

    public string Description { get; }

    public MoveChildCommand(UIScreenNode node, int newIndex)
    {
        ArgumentNullException.ThrowIfNull(node);

        _node = node;
        _oldIndex = node.Parent?.IndexOfChild(node) ?? 0;
        _newIndex = newIndex;
        Description = $"Reorder '{node.ControlType}' to position {newIndex}";
    }

    public void Execute() => _node.Parent?.MoveChild(_node, _newIndex);

    public void Undo() => _node.Parent?.MoveChild(_node, _oldIndex);
}
