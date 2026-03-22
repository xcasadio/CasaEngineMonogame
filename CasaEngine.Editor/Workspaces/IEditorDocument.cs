namespace CasaEngine.Editor.Workspaces;

public interface IEditorDocument
{
    string DocumentId { get; }

    string DisplayName { get; }

    EditorWorkspaceId WorkspaceId { get; }
}