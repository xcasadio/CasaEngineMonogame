namespace CasaEngine.EditorServices.History;

public sealed class EditorDelegateCommand : IEditorCommand
{
    private readonly Action _execute;
    private readonly Action _undo;

    public EditorDelegateCommand(string description, Action execute, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        Description = description;
    }

    public string Description { get; }

    public void Execute()
    {
        _execute();
    }

    public void Undo()
    {
        _undo();
    }
}