using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Rendering.Vector;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application.Components.DebugTools;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DragDrop;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A dockable world viewport panel backed by a hosted CasaEngine render view.
/// The editor displays the render target produced by the engine runtime instead of
/// trying to draw the world directly from the UI layer.
/// </summary>
public class WorldViewportPanel : IDisposable
{
    public event Action<Entity?>? SelectedEntityChanged;

    private sealed class ViewportHostPanel : MGDockPanel
    {
        public ViewportHostPanel(MGWindow window)
            : base(window)
        {
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            return action switch
            {
                UINavigationAction.MoveNext => true,
                UINavigationAction.MovePrevious => true,
                UINavigationAction.MoveUp => true,
                UINavigationAction.MoveDown => true,
                UINavigationAction.MoveLeft => true,
                UINavigationAction.MoveRight => true,
                _ => false,
            };
        }
    }

    private sealed class MguiViewportViewHost : IViewHost, IViewScreenBoundsHost
    {
        private readonly Func<Rectangle> _getScreenBounds;
        private bool _disposed;

        public MguiViewportViewHost(ViewId viewId, Func<Rectangle> getScreenBounds)
        {
            ViewId = viewId;
            _getScreenBounds = getScreenBounds;
        }

        public ViewId ViewId { get; }

        public int Width => ScreenBounds.Width;

        public int Height => ScreenBounds.Height;

        public bool IsVisible => ScreenBounds.Width > 0 && ScreenBounds.Height > 0;

        public Rectangle ScreenBounds => _getScreenBounds();

        public event Action<IViewHost, int, int>? Resized;

        public event Action<IViewHost>? Closed;

        public void NotifyResized(int newWidth, int newHeight)
        {
            Resized?.Invoke(this, newWidth, newHeight);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Closed?.Invoke(this);
        }
    }

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly IWindowInputSource _windowInputSource;

    private MGDockPanel _viewportHost = null!;
    private MGImage _viewportImage = null!;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiViewportViewHost? _renderViewHost;
    private DebugGridComponent? _grid;
    private DebugAxisComponent? _axis;
    private IEditorVectorCanvas? _vectorCanvas;
    private Texture2D? _boundTexture;
    private World? _fallbackWorld;
    private World? _observedWorld;
    private World? _renderWorldOverride;
    private WorldEnvironmentSettings? _environmentOverride;
    private Entity? _cameraEntity;
    private Entity? _selectedEntity;
    private ArcBallCameraComponent? _camera;
    private readonly EditorViewportCameraController _cameraController = new();
    private readonly EditorViewportGizmoController _gizmoController;
    private EditorViewportCameraState? _savedPrimaryWorldCameraState;
    private int _rtWidth = 16;
    private int _rtHeight = 16;
    private static readonly MGSolidFillBrush DropHighlightBrush = new(new Color(70, 130, 180, 96));

    internal WorldViewportPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, IWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _gizmoController = new EditorViewportGizmoController(editorRuntime);
        _gizmoController.SelectedEntityChanged += OnGizmoSelectedEntityChanged;
    }

    public MGElement CreateContent()
    {
        if (_viewportHost != null)
        {
            return _viewportHost;
        }

        EnsureRenderViewCreated();

        _viewportHost = new ViewportHostPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsFocusable = true,
            AllowDrop = true,
        };

        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;
        _viewportHost.DragEnter += OnViewportDragEnter;
        _viewportHost.DragOver += OnViewportDragOver;
        _viewportHost.DragLeave += OnViewportDragLeave;
        _viewportHost.Drop += OnViewportDrop;
        _viewportHost.MouseHandler.LMBPressedInside += (_, e) =>
        {
            ActivateThisView(captureInput: false);
        };
        _viewportHost.MouseHandler.LMBClickedInside += (_, e) =>
        {
            ActivateThisView(captureInput: false);
        };

        _viewportImage = new MGImage(_window, new MGTextureData(EditorIcons.AsImage(_surface!.Texture!)!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };

        if (_renderView != null && _renderView.Host != null)
        {
            _editorRuntime.GameManager.ViewManager.HookViewHost(_renderView);
        }

        RegisterViewportInput();

        _viewportHost.TryAddChild(_viewportImage, Dock.Top);

        return _viewportHost;
    }

    public void DrawViewport(GameTime gameTime)
    {
        SynchronizeRenderViewWorld();
        SynchronizeCamera();
        if (HasWorldOverride)
        {
            _gizmoController.Deactivate();
        }
        else
        {
            SynchronizeGizmo();
        }

        RefreshTextureBinding();
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(6)
        {
            $"View world: {_renderView?.World.Name ?? "<none>"}",
            $"World override: {_renderWorldOverride?.Name ?? "<none>"}",
            $"Environment override: {_renderView?.EnvironmentOverride?.BackgroundMode.ToString() ?? "<none>"}",
            $"Texture: {DescribeBoundTexture()}",
            $"Physics debug world: {DescribeLastPhysicsDebugWorld()}",
        };

        int debugBodyCount = DescribeLastPhysicsDebugObjectCount();
        if (debugBodyCount >= 0)
        {
            result.Add($"Physics debug bodies: {debugBodyCount}");
        }

        return result;
    }

    public void UpdateInput(GameTime gameTime)
    {
        if (_viewportHost == null)
        {
            return;
        }

        var router = _editorRuntime.InputComponent.InputRouter;
        bool isKeyboardFocused = _renderView != null && (router?.KeyboardFocusViewId ?? ViewId.Empty) == _renderView.Id;

        var inputContext = _editorRuntime.InputComponent.CurrentViewInputContext;
        bool receivesInput = _renderView != null && IsPointerInputRoutedToView(inputContext, _renderView.Id);
        _gizmoController.Deactivate();

        if (_camera != null)
        {
            _cameraController.Update(
                gameTime,
                _camera,
                inputContext,
                receivesInput,
                isKeyboardFocused,
                ActivateThisView,
                () => _editorRuntime.GameManager.ViewManager.ReleaseInput());
        }

        if (!HasWorldOverride)
        {
            UpdateGizmoInput(gameTime, inputContext, receivesInput, isKeyboardFocused);
        }
    }

    private static bool IsPointerInputRoutedToView(ViewInputContext inputContext, ViewId viewId)
    {
        if (viewId.IsEmpty || inputContext.ViewId != viewId)
        {
            return false;
        }

        return inputContext.RoutingState.Reason is InputRoutingReason.Pointer
            or InputRoutingReason.Capture
            or InputRoutingReason.UIPointerCapture
            or InputRoutingReason.Modal;
    }

    public void SetSelectedEntity(Entity? entity)
    {
        _selectedEntity = entity;
        _gizmoController.SetSelectedEntity(entity);
    }

    public bool HasWorldOverride => _renderWorldOverride != null;

    public void SetEnvironmentOverride(WorldEnvironmentSettings? environmentSettings)
    {
        if (ReferenceEquals(_environmentOverride, environmentSettings))
        {
            return;
        }

        _environmentOverride = environmentSettings;

        if (_renderView != null)
        {
            _renderView.EnvironmentOverride = _environmentOverride;
            _renderView.Invalidate();
        }
    }

    public void SetWorldOverride(World? world)
    {
        if (ReferenceEquals(_renderWorldOverride, world))
        {
            return;
        }

        bool enteringPreview = _renderWorldOverride == null && world != null;
        bool leavingPreview = _renderWorldOverride != null && world == null;

        if (enteringPreview)
        {
            _savedPrimaryWorldCameraState = _cameraController.CaptureState();
        }

        _renderWorldOverride = world;

        if (_renderWorldOverride != null)
        {
            if (_selectedEntity != null)
            {
                _selectedEntity = null;
                SelectedEntityChanged?.Invoke(null);
            }

            _gizmoController.SetSelectedEntity(null);
            _cameraController.SetState(MathHelper.PiOver4, -MathHelper.Pi / 6f, 4.2f, Vector3.Zero);
        }
        else if (leavingPreview && _savedPrimaryWorldCameraState.HasValue)
        {
            _cameraController.RestoreState(_savedPrimaryWorldCameraState.Value);
            _savedPrimaryWorldCameraState = null;
        }

        if (_camera != null)
        {
            _cameraController.ApplyTo(_camera);
        }

        SynchronizeRenderViewWorld();
        _renderView?.Invalidate();
    }

    public void FocusEntity(Entity? entity)
    {
        if (entity?.RootComponent == null || _camera == null)
        {
            return;
        }

        var bounds = entity.GetBoundingBox();
        var diagonal = Vector3.Distance(bounds.Min, bounds.Max);
        var distance = Math.Max(5f, diagonal <= 0f ? 10f : diagonal * 1.5f);

        _cameraController.Focus(entity.RootComponent.Position, distance);
        _cameraController.ApplyTo(_camera);
    }

    private void OnViewportDragEnter(object? sender, DragEnterEventArgs e)
    {
        var draggedItems = e.Data.GetData<List<ContentItem>>();
        bool canDrop = CanDropAssets(draggedItems);
        if (canDrop)
        {
            _viewportHost.OverlayBrush = DropHighlightBrush;
        }
    }

    private void OnViewportDragOver(object? sender, DragOverEventArgs e)
    {
        var draggedItems = e.Data.GetData<List<ContentItem>>();
        bool canDrop = CanDropAssets(draggedItems);
        e.Data.DropEffect = canDrop ? DragDropEffect.Copy : DragDropEffect.None;
        _viewportHost.OverlayBrush = canDrop ? DropHighlightBrush : null;
    }

    private void OnViewportDragLeave(object? sender, DragLeaveEventArgs e)
    {
        _viewportHost.OverlayBrush = null;
    }

    private void OnViewportDrop(object? sender, DropEventArgs e)
    {
        _viewportHost.OverlayBrush = null;
        DropAssets(e.Data.GetData<List<ContentItem>>());
    }

    private void OnViewportBoundsChanged(object? sender, EventArgs<Rectangle> e)
    {
        var newBounds = e.NewValue;
        var width = Math.Max(16, newBounds.Width);
        var height = Math.Max(16, newBounds.Height);

        if (width == _rtWidth && height == _rtHeight)
        {
            return;
        }

        _rtWidth = width;
        _rtHeight = height;

        _renderViewHost?.NotifyResized(width, height);

        _surface?.EnsureSize(width, height);
        _camera?.OnScreenResized(width, height);
        RefreshTextureBinding();
    }

    private bool CanDropAssets(IReadOnlyList<ContentItem>? draggedItems)
    {
        if (HasWorldOverride)
        {
            return false;
        }

        if (_editorRuntime.GameManager.CurrentWorld == null || !AssetCatalog.IsLoaded)
        {
            return false;
        }

        if (draggedItems == null || draggedItems.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < draggedItems.Count; index++)
        {
            if (TryResolveDroppableAsset(draggedItems[index], out _))
            {
                return true;
            }
        }

        return false;
    }

    private void DropAssets(IReadOnlyList<ContentItem>? draggedItems)
    {
        var world = _editorRuntime.GameManager.CurrentWorld;
        if (world == null || draggedItems == null || draggedItems.Count == 0)
        {
            return;
        }

        var createdEntities = new List<Entity>();

        for (int index = 0; index < draggedItems.Count; index++)
        {
            if (!TryResolveDroppableAsset(draggedItems[index], out var assetInfo))
            {
                continue;
            }

            var entity = CreateEntityForDroppedAsset(draggedItems[index], assetInfo, createdEntities.Count);
            createdEntities.Add(entity);
        }

        if (createdEntities.Count == 0)
        {
            return;
        }

        var previousSelection = _selectedEntity;
        var lastCreatedEntity = createdEntities[^1];
        string description = createdEntities.Count == 1
            ? "Drop Asset Entity"
            : $"Drop {createdEntities.Count} Asset Entities";

        ExecuteWorldCommand(
            description,
            () =>
            {
                for (int index = 0; index < createdEntities.Count; index++)
                {
                    EditorWorldEditingService.AttachEntity(world, createdEntities[index], parent: null);
                }

                ApplySelection(lastCreatedEntity);
            },
            () =>
            {
                for (int index = createdEntities.Count - 1; index >= 0; index--)
                {
                    EditorWorldEditingService.DetachEntity(world, createdEntities[index]);
                }

                ApplySelection(previousSelection);
            });
    }

    private bool TryResolveDroppableAsset(ContentItem item, out AssetInfo assetInfo)
    {
        assetInfo = null!;

        if (item.IsDirectory || !IsSupportedDropExtension(item.Extension))
        {
            return false;
        }

        if (!TryGetProjectRelativePath(item.FullPath, out var relativePath))
        {
            return false;
        }

        assetInfo = AssetCatalog.GetByFileName(relativePath)
            ?? AssetCatalog.GetByFileName(relativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (assetInfo == null && relativePath.StartsWith("Content\\", StringComparison.OrdinalIgnoreCase))
        {
            string trimmedRelativePath = relativePath.Substring("Content\\".Length);
            assetInfo = AssetCatalog.GetByFileName(trimmedRelativePath)
                ?? AssetCatalog.GetByFileName(trimmedRelativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (assetInfo == null)
        {
            string normalizedRelativePath = NormalizeAssetPath(relativePath);
            foreach (var candidate in AssetCatalog.AssetInfos)
            {
                if (string.Equals(NormalizeAssetPath(candidate.FileName), normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    assetInfo = candidate;
                    break;
                }
            }
        }

        return assetInfo != null;
    }

    private static bool IsSupportedDropExtension(string extension)
    {
        return string.Equals(extension, Constants.FileNameExtensions.StaticModel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, Constants.FileNameExtensions.Entity, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private bool TryGetProjectRelativePath(string fullPath, out string relativePath)
    {
        relativePath = string.Empty;

        string projectPath = EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath);
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return false;
        }

        string normalizedProjectPath = Path.GetFullPath(projectPath);
        string normalizedFullPath = Path.GetFullPath(fullPath);
        string projectRootWithSeparator = normalizedProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!normalizedFullPath.StartsWith(projectRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedFullPath, normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = Path.GetRelativePath(normalizedProjectPath, normalizedFullPath);
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private Entity CreateStaticModelEntity(AssetInfo assetInfo, int dropIndex)
    {
        Vector3 spawnPosition = GetDropSpawnPosition(dropIndex);
        var entity = new Entity
        {
            Name = Path.GetFileNameWithoutExtension(assetInfo.FileName),
        };

        var staticModelComponent = new StaticModelComponent
        {
            StaticModelAssetId = assetInfo.Id,
            Position = spawnPosition,
        };

        entity.RootComponent = staticModelComponent;
        return entity;
    }

    private Entity CreateEntityForDroppedAsset(ContentItem item, AssetInfo assetInfo, int dropIndex)
    {
        if (string.Equals(item.Extension, Constants.FileNameExtensions.Entity, StringComparison.OrdinalIgnoreCase))
        {
            return CreateEntityAssetEntity(assetInfo, dropIndex);
        }

        return CreateStaticModelEntity(assetInfo, dropIndex);
    }

    private Entity CreateEntityAssetEntity(AssetInfo assetInfo, int dropIndex)
    {
        var entityReference = EntityReference.CreateFromAssetInfo(assetInfo, _editorRuntime.AssetContentManager);
        var entity = entityReference.Entity;
        Vector3 spawnPosition = GetDropSpawnPosition(dropIndex);

        entity.Name = string.IsNullOrWhiteSpace(entity.Name)
            ? Path.GetFileNameWithoutExtension(assetInfo.FileName)
            : entity.Name;

        if (entity.RootComponent != null)
        {
            entity.RootComponent.Position = spawnPosition;
        }

        return entity;
    }

    private void ExecuteWorldCommand(string description, Action execute, Action undo)
    {
        EditorHistoryService.Current.Execute(
            new EditorHistoryContext(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport),
            new EditorDelegateCommand(description, execute, undo));
    }

    private void ApplySelection(Entity? entity)
    {
        _selectedEntity = entity;
        _gizmoController.SetSelectedEntity(entity);
        SelectedEntityChanged?.Invoke(entity);
    }

    private Vector3 GetDropSpawnPosition(int dropIndex)
    {
        Vector3 basePosition = _camera?.Target ?? _cameraController.Target;
        Vector3 right = _camera?.Right ?? Vector3.Right;

        if (right.LengthSquared() <= 0.0001f)
        {
            right = Vector3.Right;
        }
        else
        {
            right.Normalize();
        }

        return basePosition + right * (dropIndex * 2.5f);
    }

    private void EnsureRenderViewCreated()
    {
        if (_renderView != null)
        {
            return;
        }

        var world = GetRenderWorld();

        _cameraEntity = new Entity
        {
            Name = "EditorViewportCamera",
            IsVisible = false,
        };

        _camera = _cameraController.CreateCameraComponent();

        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(world);
        _camera.OnScreenResized(_rtWidth, _rtHeight);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _rtWidth,
            _rtHeight,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        var viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "World Viewport",
            World = world,
            Camera = _camera,
            Surface = _surface,
            ClearColor = Color.DimGray,
            EnvironmentOverride = _environmentOverride,
            UpdateMode = ViewUpdateMode.RealTime,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The world viewport could not create its render view.");
        }

        _renderView = renderView;
        // Only report real bounds when the element is in the visual tree (active dock tab).
        // When the viewport is a background tab, SetParent(null) detaches it but LayoutBounds
        // retains its last value → ScreenToView would incorrectly route input to this view.
        _renderViewHost = new MguiViewportViewHost(renderView.Id,
            () => _viewportHost?.Parent != null ? _viewportHost.LayoutBounds : Rectangle.Empty);
        _renderView.Host = _renderViewHost;
        AttachWorld(world);
        EnsureEditorOverlays(world);
        EnsureEditorGizmo(world);
        _gizmoController.SetSelectedEntity(_selectedEntity);
    }

    private void RegisterViewportInput()
    {
        if (_renderView == null)
        {
            return;
        }

        _editorRuntime.InputComponent.InputRouter?.RegisterViewInput(_renderView.Id, _windowInputSource);
    }

    private void ActivateThisView(bool captureInput)
    {
        if (_renderView == null)
        {
            return;
        }

        _editorRuntime.GameManager.ViewManager.SetActive(_renderView);
        _editorRuntime.InputComponent.InputRouter?.SetKeyboardFocus(_renderView.Id);
        _viewportHost?.Focus(KeyboardFocusSource.Pointer);

        if (captureInput)
        {
            _editorRuntime.GameManager.ViewManager.CaptureInput(_renderView);
        }
    }

    public void ReleaseInputIfOutside(Point screenPosition)
    {
        if (_renderView == null || _viewportHost == null)
        {
            return;
        }

        // Guard: treat detached (background-tab) viewport as if it has no bounds,
        // so clicks in the document area correctly release input / clear keyboard focus.
        if (_viewportHost.Parent != null && _viewportHost.LayoutBounds.Contains(screenPosition))
        {
            return;
        }

        _editorRuntime.GameManager.ViewManager.ReleaseInput();
        _editorRuntime.InputComponent.InputRouter?.ClearKeyboardFocus(_renderView.Id);
    }

    private World GetRenderWorld()
    {
        if (_renderWorldOverride != null)
        {
            return _renderWorldOverride;
        }

        var currentWorld = _editorRuntime.GameManager.CurrentWorld;
        if (currentWorld != null)
        {
            return currentWorld;
        }

        _fallbackWorld ??= CreateFallbackWorld();
        return _fallbackWorld;
    }

    private World CreateFallbackWorld()
    {
        var world = new World
        {
            Name = "EditorPreviewWorld",
        };

        world.LoadContent(_editorRuntime);
        return world;
    }

    private void SynchronizeRenderViewWorld()
    {
        if (_renderView == null)
        {
            return;
        }

        var desiredWorld = _renderWorldOverride ?? _editorRuntime.GameManager.CurrentWorld ?? _fallbackWorld ?? CreateFallbackWorld();
        AttachWorld(desiredWorld);
        if (ReferenceEquals(_renderView.World, desiredWorld))
        {
            return;
        }

        _renderView.World = desiredWorld;
        _cameraEntity?.InitializeWithWorld(desiredWorld);
        _camera?.OnScreenResized(_rtWidth, _rtHeight);

        _gizmoController.ResetWorld(desiredWorld);
        if (_selectedEntity?.World != desiredWorld)
        {
            _selectedEntity = null;
            SelectedEntityChanged?.Invoke(null);
        }

        _gizmoController.SetSelectedEntity(_selectedEntity);
    }

    private void AttachWorld(World world)
    {
        if (ReferenceEquals(_observedWorld, world))
        {
            return;
        }

        DetachWorld();
        _observedWorld = world;
        _observedWorld.EntityAdded += OnWorldEntityAdded;
        _observedWorld.EntityRemoved += OnWorldEntityRemoved;
        _observedWorld.EntitiesCleared += OnWorldEntitiesCleared;
    }

    private void DetachWorld()
    {
        if (_observedWorld == null)
        {
            return;
        }

        _observedWorld.EntityAdded -= OnWorldEntityAdded;
        _observedWorld.EntityRemoved -= OnWorldEntityRemoved;
        _observedWorld.EntitiesCleared -= OnWorldEntitiesCleared;
        _observedWorld = null;
    }

    private void OnWorldEntityAdded(object? sender, Entity entity)
    {
        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, _selectedEntity);
        }
    }

    private void OnWorldEntityRemoved(object? sender, Entity entity)
    {
        if (ReferenceEquals(_selectedEntity, entity))
        {
            _selectedEntity = null;
            SelectedEntityChanged?.Invoke(null);
        }

        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, _selectedEntity);
        }
    }

    private void OnWorldEntitiesCleared(object? sender, EventArgs e)
    {
        _selectedEntity = null;
        SelectedEntityChanged?.Invoke(null);

        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, null);
        }
    }

    private void OnGizmoSelectedEntityChanged(Entity? entity)
    {
        _selectedEntity = entity;
        SelectedEntityChanged?.Invoke(entity);
    }

    private void SynchronizeCamera()
    {
        if (_camera == null)
        {
            return;
        }

        _cameraController.ApplyTo(_camera);
    }

    private void RefreshTextureBinding()
    {
        var texture = _surface?.Texture;
        if (texture == null || ReferenceEquals(texture, _boundTexture))
        {
            return;
        }

        _boundTexture = texture;

        if (_viewportImage != null)
        {
            _viewportImage.Source = new MGTextureData(EditorIcons.AsImage(texture)!);
        }
    }

    private string DescribeBoundTexture()
    {
        return _boundTexture == null
            ? "<none>"
            : $"{_boundTexture.Width}x{_boundTexture.Height}";
    }

    private string DescribeLastPhysicsDebugWorld()
    {
        if (_renderView == null)
        {
            return "<none>";
        }

        return _editorRuntime.PhysicsDebugViewRendererComponent.TryGetLastRenderedPhysicsWorldName(_renderView.Id, out string worldName)
            ? worldName
            : _renderView.World.Name;
    }

    private int DescribeLastPhysicsDebugObjectCount()
    {
        if (_renderView == null)
        {
            return -1;
        }

        return _editorRuntime.PhysicsDebugViewRendererComponent.GetLastRenderedPhysicsObjectCount(_renderView.Id);
    }

    private void EnsureEditorGizmo(World world)
    {
        _gizmoController.EnsureInitialized(_renderView, _camera, _surface, world);
    }

    private void EnsureEditorOverlays(World world)
    {
        if (_renderView == null)
        {
            return;
        }

        _grid ??= CreateGridComponent();
        _axis ??= CreateAxisComponent();
        _vectorCanvas ??= CreateVectorCanvas();

        var overlayPipeline = _renderView.Pipeline as OverlayViewPipeline ?? new OverlayViewPipeline();
        overlayPipeline.RenderGridAction = (graphicsDevice, _, frame) => _grid?.DrawForView(graphicsDevice, in frame);
        overlayPipeline.RenderAxisAction = (graphicsDevice, _, frame) => _axis?.DrawForView(graphicsDevice, in frame);
        
        _renderView.Pipeline = overlayPipeline;
    }

    private DebugGridComponent CreateGridComponent()
    {
        var grid = new DebugGridComponent(_editorRuntime);
        grid.Initialize();
        return grid;
    }

    private DebugAxisComponent CreateAxisComponent()
    {
        var axis = new DebugAxisComponent(_editorRuntime);
        axis.Initialize();
        return axis;
    }

    private IEditorVectorCanvas CreateVectorCanvas()
    {
        string[] candidatePaths =
        {
            Path.Combine(AppContext.BaseDirectory, "Content", "fonts", "JetBrainsMono", "JetBrainsMono-Regular.ttf"),
            Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "JetBrainsMono", "ttf", "JetBrainsMono-Regular.ttf"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Content", "fonts", "JetBrainsMono", "JetBrainsMono-Regular.ttf"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CasaEngine.Editor", "Content", "fonts", "JetBrainsMono", "JetBrainsMono-Regular.ttf"),
        };

        foreach (string candidatePath in candidatePaths)
        {
            string fullPath = Path.GetFullPath(candidatePath);
            if (File.Exists(fullPath))
            {
                return new NvgSharpVectorCanvas(fullPath);
            }
        }

        return NullEditorVectorCanvas.Instance;
    }

    private void SynchronizeGizmo()
    {
        _gizmoController.Synchronize(_camera, _surface, _renderView?.World);
    }

    private void UpdateGizmoInput(GameTime gameTime, ViewInputContext inputContext, bool receivesInput, bool isKeyboardFocused)
    {
        _gizmoController.Update(gameTime, inputContext, receivesInput, isKeyboardFocused, _camera, _surface, _renderView?.World);
    }

    public void Dispose()
    {
        DetachWorld();

        if (_renderView != null)
        {
            _editorRuntime.InputComponent.InputRouter?.UnregisterViewInput(_renderView.Id);
            if (_renderView.Host != null)
            {
                _editorRuntime.GameManager.ViewManager.UnhookViewHost(_renderView);
                _renderView.Host = null;
            }
        }

        _renderViewHost?.Dispose();
        _renderViewHost = null;

        _gizmoController.Dispose();
        DisposeOverlayComponent(_grid);
        DisposeOverlayComponent(_axis);
        _vectorCanvas?.Dispose();
        _vectorCanvas = null;
        _grid = null;
        _axis = null;

        if (_renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _surface?.Dispose();
    }

    private void DisposeOverlayComponent(DrawableGameComponent? component)
    {
        if (component == null)
        {
            return;
        }

        _editorRuntime.Components.Remove(component);
        component.Dispose();
    }
}
