using System;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.Transform;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

public sealed class EntityAssetEditorPanel : IDisposable
{
    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly FrameCachedWindowInputSource _windowInputSource;
    private readonly WorldEnvironmentSettings _environmentOverride = PreviewEnvironmentFactory.CreateNeutralPreview(EditorThemePalette.PreviewClearColor);

    private WorldViewportPanel? _viewportPanel;
    private MGElement? _viewportContent;
    private EntityComponent? _selectedComponent;
    private Entity? _entity;
    private World? _previewWorld;
    private string? _loadedRelativePath;
    private string? _historyContextId;

    internal EntityAssetEditorPanel(
        MGWindow window,
        GraphicsDevice graphicsDevice,
        HostedEditorGameAdapter editorRuntime,
        FrameCachedWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
    }

    public Entity? LoadedEntity => _entity;

    public EntityComponent? SelectedComponent => _selectedComponent;

    public string? LoadedRelativePath => _loadedRelativePath;

    public EditorHistoryContext HistoryContext => string.IsNullOrWhiteSpace(_historyContextId)
        ? EditorHistoryContext.Empty
        : new EditorHistoryContext(EditorHistoryContextKind.Entity, _historyContextId);

    public bool IsDirty => TryGetHistoryContext(out var historyContext) && EditorDirtyStateService.Current.IsDirty(historyContext);

    public event Action<EntityComponent?>? SelectedComponentChanged;

    public MGElement CreateContent()
    {
        if (_viewportContent != null)
        {
            return _viewportContent;
        }

        _viewportPanel = new WorldViewportPanel(_window, _graphicsDevice, _editorRuntime, _windowInputSource);
        _viewportPanel.EnablePreviewSelection = true;
        _viewportPanel.SelectedEntityChanged += OnViewportSelectedEntityChanged;
        _viewportContent = _viewportPanel.CreateContent();
        BindViewport(focusEntity: true);
        return _viewportContent;
    }

    public void SetHistoryContextId(string historyContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyContextId);

        _historyContextId = historyContextId;
        if (_viewportPanel != null)
        {
            _viewportPanel.GizmoHistoryContext = HistoryContext;
        }
    }

    public void LoadAsset(Entity entity, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _entity = entity;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        SetSelectedComponent(null);
        RebuildPreviewWorld();

        if (TryGetHistoryContext(out var historyContext))
        {
            EditorDirtyStateService.Current.MarkSaved(historyContext);
        }
    }

    public bool ReloadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return false;
        }

        if (IsDirty)
        {
            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
        if (!TryLoadAsset(fullPath, out var entity))
        {
            return false;
        }

        LoadAsset(entity, fullPath);
        return true;
    }

    public bool TrySaveLoadedAsset(out string? errorMessage)
    {
        errorMessage = null;

        if (_entity == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No entity is loaded.";
            return false;
        }

        if (!IsDirty)
        {
            return true;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _entity, EditorAssetSaveSource.EntityAssetEditorPanel);

            if (TryGetHistoryContext(out var historyContext))
            {
                EditorDirtyStateService.Current.MarkSaved(historyContext);
            }

            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            errorMessage = exception.Message;

            return false;
        }
    }

    public void SetSelectedComponent(EntityComponent? component)
    {
        if (component != null && !ReferenceEquals(component.Owner, _entity))
        {
            component = null;
        }

        if (ReferenceEquals(_selectedComponent, component))
        {
            return;
        }

        _selectedComponent = component;
        SyncViewportSelection();
        SelectedComponentChanged?.Invoke(_selectedComponent);
    }

    public void Dispose()
    {
        if (_viewportPanel != null)
        {
            _viewportPanel.SelectedEntityChanged -= OnViewportSelectedEntityChanged;
        }

        _viewportPanel?.Dispose();
        _previewWorld?.Clear();
        _previewWorld = null;
    }

    internal static bool TryLoadAsset(string fullPath, out Entity entity)
    {
        entity = new Entity();

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["root_component"] == null && document["components"] == null)
            {
                return false;
            }

            entity.Load(document);
            entity.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(entity.FileName)
                            ?? AssetCatalog.GetByFileName(entity.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                entity.Name = assetInfo.Name;
                entity.AssetId = assetInfo.Id;
                entity.FileName = assetInfo.FileName;
            }
            else
            {
                entity.AssetId = entity.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RebuildPreviewWorld()
    {
        if (_previewWorld != null)
        {
            _previewWorld.Clear();
            _previewWorld = null;
        }

        if (_entity == null)
        {
            _viewportPanel?.SetWorldOverride(null);
            return;
        }

        EnsurePreviewWorldCreated();
        BindViewport(focusEntity: true);
    }

    private void EnsurePreviewWorldCreated()
    {
        if (_previewWorld != null)
        {
            return;
        }

        _previewWorld = new World
        {
            Name = "EntityAssetPreviewWorld",
        };
        _previewWorld.LoadContent(_editorRuntime);

        if (_entity != null)
        {
            _previewWorld.AddEntity(_entity);
            _previewWorld.Update(0f);
        }
    }

    private void BindViewport(bool focusEntity)
    {
        if (_viewportPanel == null)
        {
            return;
        }

        _viewportPanel.SetWorldOverride(_entity == null ? null : _previewWorld);
        _viewportPanel.SetEnvironmentOverride(_environmentOverride);
        _viewportPanel.GizmoHistoryContext = HistoryContext;
        SyncViewportSelection();
        if (focusEntity)
        {
            _viewportPanel.FocusEntity(_entity);
        }
    }

    private void SyncViewportSelection()
    {
        if (_viewportPanel == null)
        {
            return;
        }

        var transformable = GetSelectedTransformable();
        _viewportPanel.EnablePreviewGizmo = transformable != null;
        _viewportPanel.SetSelectedEntity(_entity);
        _viewportPanel.SetSelectedTransformable(transformable);
    }

    private ITransformableObject? GetSelectedTransformable()
    {
        if (_selectedComponent is SceneComponent sceneComponent)
        {
            return sceneComponent;
        }

        return _selectedComponent == null ? _entity?.RootComponent : null;
    }

    private void OnViewportSelectedEntityChanged(Entity? entity)
    {
        if (_entity == null || _viewportPanel == null)
        {
            return;
        }

        if (entity == null)
        {
            // Entity documents always keep their single preview entity as the active root selection.
            _viewportPanel.SetSelectedEntity(_entity);
            SetSelectedComponent(null);
            return;
        }

        if (ReferenceEquals(entity, _entity))
        {
            SetSelectedComponent(null);
        }
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.Entity, _historyContextId);
        return true;
    }
}