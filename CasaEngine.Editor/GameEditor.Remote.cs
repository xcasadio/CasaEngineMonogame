using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices;

namespace CasaEngine.Editor;

/// <summary>The editor side of the remote commands (ADR-0092): what they read and call on <see cref="GameEditor"/>.</summary>
public partial class GameEditor
{
    /// <summary>History context of the world edited by the remote commands (plan P5).</summary>
    internal static readonly EditorHistoryContext RemoteWorldHistoryContext = new(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport);

    private sealed class GameEditorRemoteHost : IEditorRemoteHost
    {
        private readonly GameEditor _editor;

        public GameEditorRemoteHost(GameEditor editor)
        {
            _editor = editor;
        }

        public string EditorVersion => typeof(GameEditor).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        public string ProjectFilePath => EditorProjectSession.CurrentProjectFilePath;

        public string CurrentWorldName => _editor._editorRuntime?.GameManager.CurrentWorld?.Name;

        public bool IsPlaySessionActive => _editor._playModeService?.IsPlaySessionActive == true;

        public bool HasUnsavedWorldChanges => _editor._editorDirtyState.IsDirty(RemoteWorldHistoryContext);
    }
}
