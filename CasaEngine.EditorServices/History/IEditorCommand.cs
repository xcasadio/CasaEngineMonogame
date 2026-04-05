namespace CasaEngine.EditorServices.History;

/// <summary>
/// Represents a reversible editor operation.
/// </summary>
public interface IEditorCommand
{
    /// <summary>Human-readable description used by undo/redo UI.</summary>
    string Description { get; }

    /// <summary>Applies the command.</summary>
    void Execute();

    /// <summary>Reverses the command.</summary>
    void Undo();
}