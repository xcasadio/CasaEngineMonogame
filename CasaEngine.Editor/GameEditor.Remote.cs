using System;
using System.Collections.Generic;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.Framework.Assets;
using System.IO;
using CasaEngine.Core.Logging;
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
        try
        {
            GraphicsDevice.Clear(Color.Transparent);
            _desktop?.Draw();
        }
        finally
        {
            GraphicsDevice.SetRenderTargets(previousTargets);
        }

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
        catch (Exception ex)
        {
            // Reported to the client and logged, never thrown out of Draw (verifier finding A3).
            Logs.WriteWarning($"[Remote] Capture failed: {ex}");
            request.Result = new RemoteCaptureResult { Error = $"The capture failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Contents the automation itself wrote to a file (after its own save or creation): the end-of-run restore and
    /// delete only touch a file still holding them, so a change made meanwhile through the remote control is kept
    /// (ADR-0093 P9).
    /// </summary>
    private readonly Dictionary<string, string> _automationWrittenFileContents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Runs a registered command for an automation option through the in-process executor (ADR-0093 P9).</summary>
    private bool TryRunAutomationCommand(string method, object parameters, out System.Text.Json.JsonElement result, out string error)
    {
        Editor.Remote.Protocol.RemoteResponse response = _remoteControl.Invoke(method, parameters);
        if (response.Error != null)
        {
            result = default;
            error = response.Error.Message;
            return false;
        }

        result = response.Result ?? default;
        error = null;
        return true;
    }

    /// <summary>
    /// The path rule of ADR-0093 P11 for <c>--open-asset</c> (automation and startup), <c>--content-folder</c>,
    /// <c>--create-particle-asset</c> and <c>--drop-particle-asset</c>: relative to the project, or absolute inside it
    /// (made relative); a path outside the project, holding ':' or crossing a link inside it (A3) is refused, see
    /// <see cref="EditorRemoteValidation.TryResolveAutomationPath"/>. <paramref name="relativePath"/> is empty for the
    /// project folder itself.
    /// </summary>
    private static bool TryResolveAutomationProjectPath(string optionPath, out string fullPath, out string relativePath, out string error)
    {
        fullPath = ResolveAutomationAssetPath(optionPath);
        return EditorRemoteValidation.TryResolveAutomationPath(EngineEnvironment.ProjectPath, fullPath, out relativePath, out error);
    }

    /// <summary>The asset id an asset document panel id ends with (<c>prefix + id:N</c>).</summary>
    private static bool TryGetAssetIdFromPanelId(string prefix, string panelId, out Guid assetId)
    {
        assetId = Guid.Empty;
        return panelId != null
            && panelId.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(panelId.Substring(prefix.Length), "N", out assetId);
    }

    /// <summary>Remembers what the automation wrote to <paramref name="fullPath"/>, for the end-of-run restore rule.</summary>
    private void RecordAutomationWrittenFile(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            _automationWrittenFileContents[fullPath] = File.ReadAllText(fullPath);
        }
    }

    /// <summary>
    /// True when the end of the automation may restore or delete <paramref name="fullPath"/>: the automation wrote it
    /// and it still holds what the automation wrote. A file the automation did not write (its edit was refused or not
    /// saved) is never touched; a file changed since is kept, and that is logged.
    /// </summary>
    private bool IsAutomationFileUnchangedSinceWrite(string fullPath)
    {
        if (!_automationWrittenFileContents.TryGetValue(fullPath, out string written))
        {
            return false;
        }

        if (!File.Exists(fullPath) || string.Equals(File.ReadAllText(fullPath), written, StringComparison.Ordinal))
        {
            return true;
        }

        string message = $"[Automation] Kept '{fullPath}': it changed after the automation wrote it (remote control).";
        Logs.WriteWarning(message);
        EditorDiagnosticsBuffer.Append(LogVerbosity.Warning, message);

        // The diagnostics file is written before the end-of-run restore: the line is appended to it.
        try
        {
            File.AppendAllText(ResolveAutomationDiagnosticsPath(), $"[Warning] {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logs.WriteWarning($"[Automation] Could not append to the diagnostics file: {ex.Message}");
        }

        return false;
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

        public IReadOnlyList<RemotePanelInfo> ListPanels()
        {
            var panels = new List<RemotePanelInfo>();
            if (_editor._dockHost?.LayoutModel == null)
            {
                return panels;
            }

            foreach (var group in _editor._dockHost.LayoutModel.GetAllTabGroups())
            {
                foreach (var panel in group.Panels)
                {
                    string kind = GetAssetPanelKind(panel.Id, out bool modified);
                    panels.Add(new RemotePanelInfo
                    {
                        Id = panel.Id,
                        Title = EditorRemoteWorldModel.Truncate(panel.Title),
                        Kind = kind ?? "other",
                        Active = string.Equals(group.ActivePanelId, panel.Id, StringComparison.Ordinal),
                        Modified = modified,
                    });
                }
            }

            return panels;
        }

        public bool ActivatePanel(string panelId)
        {
            if (FindPanel(panelId) == null)
            {
                return false;
            }

            _editor.ActivateDockPanel(panelId);
            return true;
        }

        /// <summary>
        /// Closing by code raises no closing event (the dock only asks for user closes), so the unsaved check is done
        /// here, per kind, and only the five asset document panels may close (ADR-0093 P7, security review S2-3; ADR-0129).
        /// </summary>
        public RemotePanelCloseResult ClosePanel(string panelId)
        {
            var panel = FindPanel(panelId);
            if (panel == null)
            {
                return RemotePanelCloseResult.NotFound;
            }

            if (GetAssetPanelKind(panelId, out bool modified) == null)
            {
                return RemotePanelCloseResult.NotClosable;
            }

            if (modified)
            {
                return RemotePanelCloseResult.Modified;
            }

            return _editor._dockHost.ClosePanel(panel) ? RemotePanelCloseResult.Closed : RemotePanelCloseResult.NotFound;
        }

        /// <remarks>
        /// For an entity asset (plan P13, security review S3-1), the panel id is the asset id, and an uncatalogued copy
        /// of the file carries that id too: the id is returned only when the panel registered under it holds
        /// <paramref name="fullPath"/> (<see cref="EditorRemoteEntityAssets.IsLoadedFile"/>), otherwise the open fails.
        /// A panel holding another file is never reloaded, and the editor's open is not reached for it.
        /// </remarks>
        public string OpenAsset(string fullPath, string type)
        {
            bool entity = type == EditorRemoteAssetTypes.Entity;

            // An open panel is brought to the front, never reloaded: reopening reads the file again and would drop
            // the unsaved edits (and, for a screen, the node ids) of earlier commands.
            AssetInfo openInfo = FindCatalogInfo(fullPath);
            if (entity && openInfo != null)
            {
                // A panel registered under this id, with or without its tab, must hold this file before the editor's
                // open is reached: that open would keep the panel's content and retitle it after this file.
                string registeredPanelId = EditorPanelIds.EntityAssetDocumentPrefix + openInfo.Id.ToString("N");
                if (_editor._entityAssetEditorPanels.ContainsKey(registeredPanelId) && !EntityPanelHoldsFile(registeredPanelId, fullPath))
                {
                    return null;
                }
            }

            string openPanelId = openInfo != null ? FindAssetPanelId(openInfo.Id) : null;
            if (openPanelId != null && (entity || !IsEntityPanelId(openPanelId)))
            {
                if (entity && !EntityPanelHoldsFile(openPanelId, fullPath))
                {
                    return null;
                }

                _editor.ActivateDockPanel(openPanelId);
                return openPanelId;
            }

            bool opened = type switch
            {
                EditorRemoteAssetTypes.Material => _editor.TryOpenMaterialAsset(fullPath),
                EditorRemoteAssetTypes.Particle => _editor.TryOpenParticleAsset(fullPath),
                EditorRemoteAssetTypes.UIScreen => _editor.TryOpenUIScreenAsset(fullPath),
                EditorRemoteAssetTypes.Sound => _editor.TryOpenSoundAsset(fullPath),
                EditorRemoteAssetTypes.Entity => _editor.TryOpenEntityAsset(fullPath),
                _ => false,
            };
            if (!opened)
            {
                return null;
            }

            AssetInfo info = FindCatalogInfo(fullPath);
            string panelId = info != null ? FindAssetPanelId(info.Id) : null;
            if (panelId == null)
            {
                return null;
            }

            if (entity)
            {
                return EntityPanelHoldsFile(panelId, fullPath) ? panelId : null;
            }

            // The four other kinds answer as before the entity panels joined the closed list.
            return IsEntityPanelId(panelId) ? null : panelId;
        }

        private static bool IsEntityPanelId(string panelId)
            => panelId.StartsWith(EditorPanelIds.EntityAssetDocumentPrefix, StringComparison.Ordinal);

        /// <summary>Plan P13: whether the entity panel registered under <paramref name="panelId"/> holds <paramref name="fullPath"/>; logs why not.</summary>
        private bool EntityPanelHoldsFile(string panelId, string fullPath)
        {
            if (!_editor._entityAssetEditorPanels.TryGetValue(panelId, out var entityPanel))
            {
                Logs.WriteWarning($"[Remote] Panel '{panelId}' is not an entity asset panel: '{fullPath}' is not opened through it.");
                return false;
            }

            if (EditorRemoteEntityAssets.IsLoadedFile(EngineEnvironment.ProjectPath, entityPanel.LoadedRelativePath, fullPath))
            {
                return true;
            }

            Logs.WriteWarning(
                $"[Remote] Entity asset panel '{panelId}' holds '{entityPanel.LoadedRelativePath ?? "<none>"}', not '{fullPath}': the open is refused.");
            return false;
        }

        private static AssetInfo FindCatalogInfo(string fullPath)
        {
            string relative = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
            return AssetCatalog.GetByFileName(relative)
                ?? AssetCatalog.GetByFileName(relative.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public bool TryGetAssetHistoryContext(Guid assetId, out EditorHistoryContext context)
        {
            string panelId = FindAssetPanelId(assetId);
            context = default;
            if (panelId == null)
            {
                return false;
            }

            EditorHistoryContextKind kind;
            if (_editor._materialInspectorPanels.ContainsKey(panelId))
            {
                kind = EditorHistoryContextKind.Material;
            }
            else if (_editor._particleInspectorPanels.ContainsKey(panelId))
            {
                kind = EditorHistoryContextKind.Particle;
            }
            else if (_editor._screenPreviewPanels.ContainsKey(panelId))
            {
                kind = EditorHistoryContextKind.UIScreen;
            }
            else if (_editor._entityAssetEditorPanels.TryGetValue(panelId, out var entityPanel))
            {
                // Plan P13, security review S3-2: the panel id is open (with its tab, FindAssetPanelId), but an
                // uncatalogued file can carry the id; the id must be a catalogued entity asset whose file the panel holds.
                AssetInfo info = AssetCatalog.Get(assetId);
                if (info == null
                    || !string.Equals(AssetInfo.InferAssetType(info.FileName), EditorRemoteAssetTypes.Entity, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath)
                    || !EditorRemoteEntityAssets.IsLoadedFile(
                        EngineEnvironment.ProjectPath,
                        entityPanel.LoadedRelativePath,
                        Path.Combine(EngineEnvironment.ProjectPath, info.FileName)))
                {
                    return false;
                }

                kind = EditorHistoryContextKind.Entity;
            }
            else
            {
                // Sounds have no history (ADR-0093).
                return false;
            }

            context = new EditorHistoryContext(kind, panelId);
            return true;
        }

        public void RefreshAfterHistory(EditorHistoryContext context) => _editor.RefreshViewsAfterHistoryCommand(context);

        public IReadOnlyList<Controls.MaterialAssetInspectorPanel.PropertySnapshot> GetMaterialProperties(Guid assetId)
        {
            string panelId = EditorPanelIds.MaterialAssetDocumentPrefix + assetId.ToString("N");
            return _editor._materialInspectorPanels.TryGetValue(panelId, out var panel) ? panel.GetPropertySnapshots() : null;
        }

        public bool TrySetMaterialProperty(
            Guid assetId,
            string property,
            string rawValue,
            Func<Framework.Materials.Definitions.MaterialPropertyDefinition, Framework.Materials.Runtime.MaterialValue, string> validate,
            out string error)
        {
            string panelId = EditorPanelIds.MaterialAssetDocumentPrefix + assetId.ToString("N");
            if (!_editor._materialInspectorPanels.TryGetValue(panelId, out var panel))
            {
                error = "The material is not open.";
                return false;
            }

            return panel.TryApplyPropertyValue(property, rawValue, validate, out error);
        }

        public void SaveAssetCatalog() => EditorAssetCatalogService.Save();

        /// <summary>The particle creation of the content browser, at a file the command reserved first (ADR-0093 P4).</summary>
        public bool CreateParticleAsset(RemoteNewAssetFile file, string preset, out Guid assetId, out string error)
        {
            assetId = Guid.Empty;
            if (!TryParseParticlePreset(preset, out var presetKind))
            {
                error = $"'{preset}' is not a particle preset.";
                return false;
            }

            EditorRemoteAssetCreation.Reserve(file.FullPath);
            var particleAsset = CreateParticleEffectAsset(file.Name, file.RelativeFile, presetKind);
            try
            {
                EditorAssetCatalogService.Add(particleAsset);
                EditorAssetWriterService.SaveAsset(file.RelativeFile, particleAsset, EditorAssetSaveSource.ParticleEffectEditorPanel);
                EditorAssetCatalogService.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                EditorAssetCatalogService.Remove(particleAsset.Id);
                File.Delete(file.FullPath);
                error = $"'{file.RelativeFile}' could not be written: {ex.Message}";
                return false;
            }

            assetId = particleAsset.Id;
            error = null;
            return true;
        }

        public Framework.Particles.Authoring.ParticleEffectAsset GetParticleAsset(Guid assetId)
            => _editor._particleInspectorPanels.TryGetValue(EditorPanelIds.ParticleAssetDocumentPrefix + assetId.ToString("N"), out var panel)
                ? panel.LoadedAsset
                : null;

        public bool TrySetParticleProperty(Guid assetId, string property, string rawValue, Func<string, string, string> validate, out string error)
        {
            if (!_editor._particleInspectorPanels.TryGetValue(EditorPanelIds.ParticleAssetDocumentPrefix + assetId.ToString("N"), out var panel))
            {
                error = "The particle asset is not open.";
                return false;
            }

            return panel.TryApplyPropertyValue(property, rawValue, validate, out error);
        }

        public EditorServices.ScreenEditor.DocumentModel.UIScreenDocument GetScreenDocument(Guid assetId)
            => _editor._screenPreviewPanels.TryGetValue(EditorPanelIds.UIScreenDocumentPrefix + assetId.ToString("N"), out var panel)
                ? panel.CurrentDocument
                : null;

        /// <summary>The path of a live inspector edit: the panel's command stack, then the preview and the screen views.</summary>
        public bool TrySetScreenProperty(Guid assetId, EditorServices.ScreenEditor.DocumentModel.UIScreenNode node, string property, string value, out string error)
        {
            string panelId = EditorPanelIds.UIScreenDocumentPrefix + assetId.ToString("N");
            if (!_editor._screenPreviewPanels.TryGetValue(panelId, out var panel))
            {
                error = "The UI screen is not open.";
                return false;
            }

            _editor.GetOrCreateScreenCommandStack(panelId).Execute(new EditorServices.ScreenEditor.Commands.SetPropertyCommand(node, property, value));
            if (!panel.TryApplyPropertyUpdate(node.Id, property, value))
            {
                panel.RefreshPreviewOnly();
            }

            _editor.RefreshScreenViews();
            error = null;
            return true;
        }

        /// <summary>The sound creation of the content browser, at a file the command reserved first (ADR-0093 P4).</summary>
        public bool CreateSoundAsset(RemoteNewAssetFile file, out Guid assetId, out string error)
        {
            assetId = Guid.Empty;
            EditorRemoteAssetCreation.Reserve(file.FullPath);
            var soundAsset = new Framework.Audio.SoundAsset
            {
                Name = file.Name,
                FileName = file.RelativeFile,
            };
            try
            {
                EditorAssetCatalogService.Add(soundAsset);
                EditorAssetWriterService.SaveAsset(file.RelativeFile, soundAsset, EditorAssetSaveSource.SoundEditorPanel);
                EditorAssetCatalogService.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                EditorAssetCatalogService.Remove(soundAsset.Id);
                File.Delete(file.FullPath);
                error = $"'{file.RelativeFile}' could not be written: {ex.Message}";
                return false;
            }

            assetId = soundAsset.Id;
            error = null;
            return true;
        }

        public Framework.Audio.SoundAsset GetSoundAsset(Guid assetId)
            => _editor._soundInspectorPanels.TryGetValue(EditorPanelIds.SoundAssetDocumentPrefix + assetId.ToString("N"), out var panel)
                ? panel.LoadedSoundAsset
                : null;

        public bool IsSoundModified(Guid assetId)
            => _editor._soundInspectorPanels.TryGetValue(EditorPanelIds.SoundAssetDocumentPrefix + assetId.ToString("N"), out var panel)
                && panel.IsDirty;

        public bool TrySetSoundProperty(Guid assetId, Action<Framework.Audio.SoundAsset> change, out string error)
        {
            if (!_editor._soundInspectorPanels.TryGetValue(EditorPanelIds.SoundAssetDocumentPrefix + assetId.ToString("N"), out var panel)
                || !panel.ApplyChange(change))
            {
                error = "The sound is not open.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Saves one asset from its open panel, as File &gt; Save does for that kind, then marks its history saved.</summary>
        public bool TrySaveAsset(Guid assetId, out string error)
        {
            string id = assetId.ToString("N");
            string materialPanel = EditorPanelIds.MaterialAssetDocumentPrefix + id;
            if (_editor._materialInspectorPanels.TryGetValue(materialPanel, out var material))
            {
                if (!material.TrySaveLoadedAsset(out error))
                {
                    return false;
                }

                _editor._editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.Material, materialPanel));
                return true;
            }

            string particlePanel = EditorPanelIds.ParticleAssetDocumentPrefix + id;
            if (_editor._particleInspectorPanels.TryGetValue(particlePanel, out var particle))
            {
                if (!particle.TrySaveLoadedAsset(out error))
                {
                    return false;
                }

                _editor._editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.Particle, particlePanel));
                return true;
            }

            string soundPanel = EditorPanelIds.SoundAssetDocumentPrefix + id;
            if (_editor._soundInspectorPanels.TryGetValue(soundPanel, out var sound))
            {
                return sound.TrySaveLoadedAsset(out error);
            }

            // As project.save does for the entity panels (SaveDirtyEntityAssetEditors), for a panel open with its tab only.
            string entityPanelId = EditorPanelIds.EntityAssetDocumentPrefix + id;
            if (_editor._entityAssetEditorPanels.TryGetValue(entityPanelId, out var entity) && FindPanel(entityPanelId) != null)
            {
                if (!entity.TrySaveLoadedAsset(out error))
                {
                    return false;
                }

                _editor._editorDirtyState.MarkSaved(new EditorHistoryContext(EditorHistoryContextKind.Entity, entityPanelId));
                _editor.UpdateDockPanelTitle(entityPanelId, _editor.GetEntityDocumentTitle(entityPanelId));
                return true;
            }

            error = "The asset is not open.";
            return false;
        }

        /// <summary>The id of the open panel of an asset among the five asset panel kinds, or null.</summary>
        private string FindAssetPanelId(Guid assetId)
        {
            string[] candidates =
            {
                EditorPanelIds.MaterialAssetDocumentPrefix + assetId.ToString("N"),
                EditorPanelIds.ParticleAssetDocumentPrefix + assetId.ToString("N"),
                EditorPanelIds.UIScreenDocumentPrefix + assetId.ToString("N"),
                EditorPanelIds.SoundAssetDocumentPrefix + assetId.ToString("N"),
                EditorPanelIds.EntityAssetDocumentPrefix + assetId.ToString("N"),
            };

            foreach (string candidate in candidates)
            {
                if (GetAssetPanelKind(candidate, out _) != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>The kind of an open asset panel of the closed list, with its unsaved state; null for any other panel.</summary>
        private string GetAssetPanelKind(string panelId, out bool modified)
        {
            if (_editor._materialInspectorPanels.TryGetValue(panelId, out var material))
            {
                modified = material.IsDirty || _editor._editorDirtyState.IsDirty(new EditorHistoryContext(EditorHistoryContextKind.Material, panelId));
                return EditorRemoteAssetTypes.Material;
            }

            if (_editor._particleInspectorPanels.TryGetValue(panelId, out var particle))
            {
                modified = particle.IsDirty || _editor._editorDirtyState.IsDirty(new EditorHistoryContext(EditorHistoryContextKind.Particle, panelId));
                return EditorRemoteAssetTypes.Particle;
            }

            if (_editor._screenPreviewPanels.ContainsKey(panelId))
            {
                modified = _editor._editorDirtyState.IsDirty(new EditorHistoryContext(EditorHistoryContextKind.UIScreen, panelId));
                return EditorRemoteAssetTypes.UIScreen;
            }

            if (_editor._soundInspectorPanels.TryGetValue(panelId, out var sound))
            {
                modified = sound.IsDirty;
                return EditorRemoteAssetTypes.Sound;
            }

            // An entity panel registered without its dock tab (the tab could not be created) is not open (security review S3-6).
            if (_editor._entityAssetEditorPanels.TryGetValue(panelId, out var entity) && FindPanel(panelId) != null)
            {
                modified = entity.IsDirty;
                return EditorRemoteAssetTypes.Entity;
            }

            modified = false;
            return null;
        }

        private MGUI.Core.UI.Docking.DockLayout.DockPanelNode FindPanel(string panelId)
        {
            if (_editor._dockHost?.LayoutModel == null)
            {
                return null;
            }

            foreach (var group in _editor._dockHost.LayoutModel.GetAllTabGroups())
            {
                foreach (var panel in group.Panels)
                {
                    if (string.Equals(panel.Id, panelId, StringComparison.Ordinal))
                    {
                        return panel;
                    }
                }
            }

            return null;
        }

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
