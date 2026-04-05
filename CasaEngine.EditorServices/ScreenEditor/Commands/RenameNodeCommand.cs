using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Reversible command that renames a <see cref="UIScreenNode"/>.
/// </summary>
public sealed class RenameNodeCommand : IUIScreenCommand
{
    private readonly UIScreenNode _node;
    private readonly string? _newName;
    private readonly string? _previousName;

    public string Description { get; }

    public RenameNodeCommand(UIScreenNode node, string? newName)
    {
        ArgumentNullException.ThrowIfNull(node);

        _node = node;
        _newName = string.IsNullOrWhiteSpace(newName) ? null : newName.Trim();
        _previousName = node.Name;
        Description = $"Rename '{node.ControlType}'";
    }

    public void Execute() => _node.Name = _newName;

    public void Undo() => _node.Name = _previousName;
}