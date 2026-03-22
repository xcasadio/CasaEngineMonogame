using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Reversible command that sets one property value on a <see cref="UIScreenNode"/>.
/// </summary>
public sealed class SetPropertyCommand : IUIScreenCommand
{
    private readonly UIScreenNode _node;
    private readonly string _propertyName;
    private readonly string? _newValue;
    private readonly string? _previousValue;

    public string Description { get; }

    public SetPropertyCommand(UIScreenNode node, string propertyName, string? newValue)
    {
        _node         = node;
        _propertyName = propertyName;
        _newValue     = newValue;

        // Capture the existing value before the command is executed
        _previousValue = node.Properties.TryGetValue(propertyName, out var existing)
            ? existing.SerializedValue
            : null;

        Description = $"Set {propertyName} = {newValue ?? "(none)"}";
    }

    public void Execute()   => _node.SetProperty(_propertyName, _newValue);
    public void Undo()      => _node.SetProperty(_propertyName, _previousValue);
}
