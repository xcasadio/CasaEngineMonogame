using System;
using System.IO;
using CasaEngine.Engine.Environment;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.Remote;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.PlayMode;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor;

/// <summary>The editor side of the remote commands (ADR-0092): what they read and call on <see cref="GameEditor"/>.</summary>
public partial class GameEditor
{
    private sealed class RemoteCaptureRequest
    {
        public string Kind { get; init; }

        public string Path { get; init; }

        public RemoteCaptureResult Result { get; set; }
    }

    private RemoteCaptureRequest _remoteCapture;

    /// <summary>Draws the MGUI desktop into a render target and writes it as PNG (automation and remote captures).</summary>
    private void SaveDesktopAsPng(string path, FileMode mode, out int width, out int height)
    {
        width = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferWidth);
        height = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferHeight);

        using var renderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        var previousTargets = GraphicsDevice.GetRenderTargets();
        GraphicsDevice.SetRenderTarget(renderTarget);
        GraphicsDevice.Clear(Color.Transparent);
        _desktop?.Draw();
        GraphicsDevice.SetRenderTargets(previousTargets);

        using var fileStream = new FileStream(path, mode, FileAccess.Write, FileShare.None);
        renderTarget.SaveAsPng(fileStream, width, height);
    }

    /// <summary>Called at the end of <see cref="Draw"/>: takes the pending remote capture, if any (ADR-0092).</summary>
    private void ProcessRemoteCapture()
    {
        RemoteCaptureRequest request = _remoteCapture;
        if (request == null || request.Result != null)
        {
            return;
        }

        try
        {
            int width;
            int height;
            if (request.Kind == EditorRemoteViewCommands.ViewportCapture)
            {
                RenderTarget2D viewport = _worldViewportPanel?.ViewportRenderTarget;
                if (viewport == null || viewport.IsDisposed)
                {
                    request.Result = new RemoteCaptureResult { Error = "The world viewport has not drawn anything yet." };
                    return;
                }

                width = viewport.Width;
                height = viewport.Height;
                using var fileStream = new FileStream(request.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                viewport.SaveAsPng(fileStream, width, height);
            }
            else
            {
                SaveDesktopAsPng(request.Path, FileMode.CreateNew, out width, out height);
            }

            request.Result = new RemoteCaptureResult { Succeeded = true, Width = width, Height = height };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            request.Result = new RemoteCaptureResult { Error = $"The capture failed: {ex.Message}" };
        }
    }

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

        public string PlayState => (_editor._playModeService?.State ?? EditorPlayModeState.Editing).ToString().ToLowerInvariant();

        public void StartPlay() => _editor.StartPlayMode();

        public void StopPlay() => _editor.StopPlayMode();

        public bool RequestCapture(string kind, string path, out string error)
        {
            if (_editor._remoteCapture is { Result: null })
            {
                error = "Another capture is in progress.";
                return false;
            }

            _editor._remoteCapture = new RemoteCaptureRequest { Kind = kind, Path = path };
            error = null;
            return true;
        }

        public bool TryTakeCaptureResult(string path, out RemoteCaptureResult result)
        {
            RemoteCaptureRequest request = _editor._remoteCapture;
            if (request == null || request.Result == null || !string.Equals(request.Path, path, StringComparison.Ordinal))
            {
                result = null;
                return false;
            }

            result = request.Result;
            _editor._remoteCapture = null;
            return true;
        }
    }
}
