namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Represents a reversible editing operation on a <see cref="DocumentModel.UIScreenDocument"/>.
/// </summary>
public interface IUIScreenCommand
{
    /// <summary>Human-readable description used in the undo/redo menu.</summary>
    string Description { get; }

    /// <summary>Applies the command.</summary>
    void Execute();

    /// <summary>Reverses the command.</summary>
    void Undo();
}
