using System;
using System.IO;
using CasaEngine.Engine.Environment;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.Remote;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.EditorServices;

namespace CasaEngine.Editor;

/// <summary>The editor side of the remote commands (ADR-0092): what they read and call on <see cref="GameEditor"/>.</summary>
public partial class GameEditor
{
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

        public bool HasUnsavedWorldChanges => _editor._editorDirtyState.IsDirty(EditorRemoteEditCommands.WorldContext);

        public World CurrentWorld => _editor._editorRuntime?.GameManager.CurrentWorld;

        public Entity SelectedEntity => EditorSelection.Current.SelectedEntity;

        public EntityComponent SelectedComponent => EditorSelection.Current.SelectedComponent;

        public LoggerEditor Logger => _editor._loggerEditor;

        public string ProjectDirectory => string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath) ? null : EngineEnvironment.ProjectPath;

        public void Select(Entity entity, EntityComponent component)
        {
            if (entity == null)
            {
                EditorSelection.Current.Clear();
            }
            else if (component != null)
            {
                EditorSelection.Current.SetSelectedComponent(component);
            }
            else
            {
                EditorSelection.Current.SetSelectedEntity(entity);
                EditorSelection.Current.ClearSelectedComponent();
            }

            _editor.RefreshWorldSelectionViews();
        }

        public void RefreshWorldViews() => _editor.RefreshWorldSelectionViews();

        /// <summary>
        /// <see cref="GameEditor.SaveCurrentProject"/> reports its failures only in the log; a world still marked
        /// modified afterwards means something was not written (plan P10).
        /// </summary>
        public bool SaveProject(out string error)
        {
            _editor.SaveCurrentProject();
            if (_editor._editorDirtyState.IsDirty(EditorRemoteEditCommands.WorldContext))
            {
                error = "The world still has unsaved changes after the save: see logs.read for the reason.";
                return false;
            }

            error = null;
            return true;
        }

        public bool CreateWorld(string worldName, string fileName, out string error)
        {
            try
            {
                var world = new World
                {
                    Name = worldName,
                    FileName = fileName,
                };

                EditorWorldWriter.SaveWorld(world);
                EditorAssetCatalogService.Add(world);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"'{fileName}' could not be written: {ex.Message}";
                return false;
            }
        }

        public bool BeginOpenWorld(string fullPath) => _editor.OpenWorldAsset(fullPath, unsavedChangesHandled: true);
    }
}
