using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Rendering.Environment;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.Transform;
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
    private readonly PreviewWorldDriver _previewWorldDriver;

    private WorldViewportPanel _viewportPanel;
    private MGElement _viewportContent;
    private Entity _selectedEntity;
    private EntityComponent _selectedComponent;
    private Entity _entity;
    private string _loadedRelativePath;
    private string _historyContextId;

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
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "EntityAssetPreviewWorld",
            UpdateMode = PreviewWorldUpdateMode.Continuous,
        });
    }

    public Entity LoadedEntity => _entity;

    public Entity SelectedEntity => _selectedEntity ?? _entity;

    public EntityComponent SelectedComponent => _selectedComponent;

    public string LoadedRelativePath => _loadedRelativePath;

    public EditorHistoryContext HistoryContext => string.IsNullOrWhiteSpace(_historyContextId)
        ? EditorHistoryContext.Empty
        : new EditorHistoryContext(EditorHistoryContextKind.Entity, _historyContextId);

    public bool IsDirty => TryGetHistoryContext(out var historyContext) && EditorDirtyStateService.Current.IsDirty(historyContext);

    public event Action<Entity> SelectedEntityChanged;

    public event Action<EntityComponent> SelectedComponentChanged;

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
        SetSelection(entity, null);
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

    public bool TrySaveLoadedAsset(out string errorMessage)
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

    public void SetSelectedComponent(EntityComponent component)
    {
        if (component != null && !ContainsEntity(component.Owner))
        {
            component = null;
        }

        SetSelection(component?.Owner ?? SelectedEntity, component);
    }

    public void SetSelectedEntity(Entity entity)
    {
        SetSelection(entity, _selectedComponent);
    }

    public void FocusEntity(Entity entity)
    {
        _viewportPanel?.FocusEntity(entity);
    }

    public void Update(GameTime gameTime)
    {
        _previewWorldDriver.Tick(gameTime);
    }

    public void UpdateInput(GameTime gameTime, bool editorShellCapturesKeyboard = false, bool editorShellBlocksPointer = false)
    {
        _viewportPanel?.UpdateInput(gameTime, editorShellCapturesKeyboard, editorShellBlocksPointer);
    }

    public void DrawViewport(GameTime gameTime)
    {
        _viewportPanel?.DrawViewport(gameTime);
    }

    public void Dispose()
    {
        if (_viewportPanel != null)
        {
            _viewportPanel.SelectedEntityChanged -= OnViewportSelectedEntityChanged;
        }

        _viewportPanel?.Dispose();
        _previewWorldDriver.Dispose();
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(8)
        {
            $"Loaded entity: {DescribeEntity(_entity)}",
            $"Selected entity: {DescribeEntity(SelectedEntity)}",
            $"Selected component: {DescribeComponent(_selectedComponent)}",
            $"Loaded path: {_loadedRelativePath ?? "<none>"}",
            $"History context: {(HistoryContext.IsEmpty ? "<empty>" : $"{HistoryContext.Kind}:{HistoryContext.Id}")}",
        };

        var viewportStates = _viewportPanel?.GetAutomationStateSnapshot();
        if (viewportStates != null)
        {
            for (int index = 0; index < viewportStates.Count; index++)
            {
                result.Add($"Viewport {viewportStates[index]}");
            }
        }

        return result;
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
        _previewWorldDriver.Clear();

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
        if (_previewWorldDriver.World != null || _entity == null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world =>
        {
            PreviewWorldLightRig.AddDefaultLights(world);
            world.AddEntity(_entity);
        });
    }

    private void BindViewport(bool focusEntity)
    {
        if (_viewportPanel == null)
        {
            return;
        }

        _viewportPanel.SetWorldOverride(_entity == null ? null : _previewWorldDriver.World);
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
        _viewportPanel.SetSelectedEntity(SelectedEntity);
        _viewportPanel.SetSelectedTransformable(transformable);
    }

    private ITransformableObject GetSelectedTransformable()
    {
        if (_selectedComponent is SceneComponent sceneComponent)
        {
            return sceneComponent;
        }

        return _selectedComponent == null ? SelectedEntity?.RootComponent : null;
    }

    private void OnViewportSelectedEntityChanged(Entity entity)
    {
        if (_entity == null)
        {
            return;
        }

        if (entity == null)
        {
            SetSelection(_entity, null);
            return;
        }

        if (ContainsEntity(entity))
        {
            SetSelection(entity, null);
        }
    }

    private void SetSelection(Entity entity, EntityComponent component)
    {
        Entity normalizedEntity = NormalizeSelectedEntity(entity);
        if (component != null && !ReferenceEquals(component.Owner, normalizedEntity))
        {
            component = null;
        }

        bool entityChanged = !ReferenceEquals(_selectedEntity, normalizedEntity);
        bool componentChanged = !ReferenceEquals(_selectedComponent, component);
        if (!entityChanged && !componentChanged)
        {
            return;
        }

        _selectedEntity = normalizedEntity;
        _selectedComponent = component;
        SyncViewportSelection();

        if (entityChanged)
        {
            SelectedEntityChanged?.Invoke(_selectedEntity);
        }

        if (componentChanged)
        {
            SelectedComponentChanged?.Invoke(_selectedComponent);
        }
    }

    private Entity NormalizeSelectedEntity(Entity entity)
    {
        if (_entity == null)
        {
            return null;
        }

        if (entity == null || !ContainsEntity(entity))
        {
            return _entity;
        }

        return entity;
    }

    private bool ContainsEntity(Entity entity)
    {
        if (_entity == null || entity == null)
        {
            return false;
        }

        return ContainsEntity(_entity, entity);
    }

    private static bool ContainsEntity(Entity current, Entity candidate)
    {
        if (ReferenceEquals(current, candidate))
        {
            return true;
        }

        foreach (var child in current.Children)
        {
            if (ContainsEntity(child, candidate))
            {
                return true;
            }
        }

        return false;
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

    private static string DescribeEntity(Entity entity)
    {
        return entity == null
            ? "<null>"
            : $"'{entity.Name}'";
    }

    private static string DescribeComponent(EntityComponent component)
    {
        if (component == null)
        {
            return "<null>";
        }

        return $"'{component.GetType().Name}' owner={DescribeEntity(component.Owner)}";
    }
}