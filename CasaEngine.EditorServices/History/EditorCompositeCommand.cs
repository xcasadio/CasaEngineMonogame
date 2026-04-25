namespace CasaEngine.EditorServices.History;

/// <summary>
/// Groups multiple <see cref="IEditorCommand"/> instances into a single undoable operation.
/// </summary>
public sealed class EditorCompositeCommand : IEditorCommand
{
    private readonly IEditorCommand[] _commands;

    public string Description { get; }

    public EditorCompositeCommand(string description, params IEditorCommand[] commands)
        : this(description, (IEnumerable<IEditorCommand>)commands)
    {
    }

    public EditorCompositeCommand(string description, IEnumerable<IEditorCommand> commands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(commands);

        Description = description;
        _commands = commands as IEditorCommand[] ?? [.. commands];
    }

    public void Execute()
    {
        for (int index = 0; index < _commands.Length; index++)
        {
            _commands[index].Execute();
        }
    }

    public void Undo()
    {
        for (int index = _commands.Length - 1; index >= 0; index--)
        {
            _commands[index].Undo();
        }
    }
}