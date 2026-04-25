using CasaEngine.EditorServices.History;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Groups multiple <see cref="IUIScreenCommand"/> instances into a single undoable/redoable operation.
/// <para/>
/// All sub-commands are executed in order; undone in reverse order.
/// </summary>
public sealed class CompositeCommand : IUIScreenCommand
{
    private readonly EditorCompositeCommand _inner;

    public string Description => _inner.Description;

    public CompositeCommand(string description, params IUIScreenCommand[] commands)
    {
        _inner = new EditorCompositeCommand(description, commands);
    }

    public CompositeCommand(string description, IEnumerable<IUIScreenCommand> commands)
    {
        _inner = new EditorCompositeCommand(description, commands);
    }

    public void Execute() => _inner.Execute();

    public void Undo() => _inner.Undo();
}
