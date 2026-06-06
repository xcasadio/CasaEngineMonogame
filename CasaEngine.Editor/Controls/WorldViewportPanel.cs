using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Overlays;
using CasaEngine.Editor.Styling;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.EditorServices.Particles;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application.Components.DebugTools;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scene.Transform;
using GizmoTools;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DragDrop;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
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
    private readonly record struct EntityDeletion(Entity Entity, Entity? Parent);

    public event Action<Entity?>? SelectedEntityChanged;

    public bool EnablePreviewSelection { get; set; }

    public bool EnablePreviewGizmo { get; set; }

    public bool ShowEditorOverlays { get; set; } = true;

    public bool UseFront2dCamera { get; set; }

    public bool ShowGizmoToolbar { get; set; }

    public EditorHistoryContext GizmoHistoryContext
    {
        get => _gizmoController.HistoryContext;
        set => _gizmoController.HistoryContext = value;
    }

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

    private MGDockPanel _root = null!;
    private MGDockPanel _viewportHost = null!;
    private MGImage _viewportImage = null!;
    private MGToggleButton? _translateToggleButton;
    private MGToggleButton? _rotateToggleButton;
    private MGToggleButton? _scaleToggleButton;
    private MGToggleButton? _worldSpaceToggleButton;
    private MGToggleButton? _localSpaceToggleButton;
    private bool _suspendToolbarCallbacks;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiViewportViewHost? _renderViewHost;
    private GridComponent? _grid;
    private AxisComponent? _axis;
    private readonly EditorLightOverlayCollector _lightOverlayCollector = new();
    private readonly EditorParticleOverlayCollector _particleOverlayCollector = new();
    private EditorLightBillboardOverlayRenderer? _lightBillboardOverlayRenderer;
    private EditorLightWireOverlayRenderer? _lightWireOverlayRenderer;
    private EditorParticleWireOverlayRenderer? _particleWireOverlayRenderer;
    private Action<GraphicsDevice, RenderView, RenderFrame>? _externalVectorOverlayAction;
    private Action<GraphicsDevice, RenderView, RenderFrame>? _externalUiOverlayAction;
    private int _lastActiveDirectionalLightCount;
    private int _lastActivePointLightCount;
    private int _lastActiveSpotLightCount;
    private Texture2D? _boundTexture;
    private World? _fallbackWorld;
    private World? _observedWorld;
    private World? _renderWorldOverride;
    private WorldEnvironmentSettings? _environmentOverride;
    private Entity? _cameraEntity;
    private Entity? _selectedEntity;
    private EntityComponent? _selectedComponent;
    private ITransformableObject? _selectedTransformable;
    private ArcBallCameraComponent? _camera;
    private readonly EditorViewportCameraController _cameraController = new();
    private readonly EditorViewportGizmoController _gizmoController;
    private EditorViewportCameraState? _savedPrimaryWorldCameraState;
    private BoundingBox? _front2dFocusedBounds;
    private float _front2dPixelsPerWorldUnit;
    private int _rtWidth = 16;
    private int _rtHeight = 16;
    private static readonly MGSolidFillBrush DropHighlightBrush = new(EditorThemePalette.DropHighlight);
    private const float MinFront2dPixelsPerWorldUnit = 0.01f;
    private const float MaxFront2dPixelsPerWorldUnit = 1024.0f;

    internal WorldViewportPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, IWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _gizmoController = new EditorViewportGizmoController(editorRuntime);
        _gizmoController.SelectedEntityChanged += OnGizmoSelectedEntityChanged;
        _gizmoController.DeleteEntitiesRequested += OnGizmoDeleteEntitiesRequested;
        _gizmoController.ActiveModeChanged += OnGizmoActiveModeChanged;
        _gizmoController.ActiveSpaceChanged += OnGizmoActiveSpaceChanged;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderViewCreated();

        _root = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        if (ShowGizmoToolbar)
        {
            _root.TryAddChild(BuildGizmoToolbar(), Dock.Top);
        }

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
        _viewportHost.MouseHandler.PressedInside += (_, e) =>
        {
            ActivateThisView(captureInput: false);
        };
        _viewportHost.MouseHandler.LMBClickedInside += (_, e) =>
        {
            ActivateThisView(captureInput: false);

            if (EnablePreviewSelection && !EnablePreviewGizmo)
            {
                SelectPreviewEntityRoot();
            }
        };
        _viewportHost.MouseHandler.Scrolled += OnViewportScrolled;

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

        _root.TryAddChild(_viewportHost, Dock.Top);
        SynchronizeGizmoToolbarState();

        return _root;
    }

    public void DrawViewport(GameTime gameTime)
    {
        SynchronizeRenderViewWorld();
        SynchronizeCamera();
        if (!ShouldSynchronizeGizmo())
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
            $"Forward directional lights: {_lastActiveDirectionalLightCount}",
            $"Forward point lights: {_lastActivePointLightCount}",
            $"Forward spot lights: {_lastActiveSpotLightCount}",
            $"Gizmo mode: {_gizmoController.ActiveMode}",
            $"Gizmo space: {_gizmoController.ActiveSpace}",
            $"Light gizmos: {_lightOverlayCollector.Items.Count}",
            $"Light billboard gizmos: {_lightBillboardOverlayRenderer?.LastDrawnItemCount ?? 0}",
            $"Light wire gizmos: {_lightWireOverlayRenderer?.LastDrawnItemCount ?? 0}",
            $"Light gizmo lines: {_lightWireOverlayRenderer?.LastDrawnLineCount ?? 0}",
            $"Particle gizmos: {_particleWireOverlayRenderer?.LastDrawnItemCount ?? 0}",
            $"Particle gizmo lines: {_particleWireOverlayRenderer?.LastDrawnLineCount ?? 0}",
        };

        int debugBodyCount = DescribeLastPhysicsDebugObjectCount();
        if (debugBodyCount >= 0)
        {
            result.Add($"Physics debug bodies: {debugBodyCount}");
        }

        return result;
    }

    public void UpdateInput(GameTime gameTime, bool editorShellCapturesKeyboard = false, bool editorShellBlocksPointer = false)
    {
        if (_viewportHost == null)
        {
            return;
        }

        var router = _editorRuntime.InputComponent.InputRouter;
        bool isBlockedByEditorModal = IsBlockedByEditorModal();

        if (isBlockedByEditorModal && _renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.ReleaseInput();
            router?.ClearKeyboardFocus(_renderView.Id);
        }

        bool isKeyboardFocused = !editorShellCapturesKeyboard
            && !isBlockedByEditorModal
            && _renderView != null
            && (router?.KeyboardFocusViewId ?? ViewId.Empty) == _renderView.Id;

        var inputContext = _editorRuntime.InputComponent.CurrentViewInputContext;
        bool receivesInput = !editorShellBlocksPointer
            && !isBlockedByEditorModal
            && _renderView != null
            && IsPointerInputRoutedToView(inputContext, _renderView.Id);
        _gizmoController.Deactivate();

        if (_camera != null)
        {
            _cameraController.Update(
                gameTime,
                _camera,
                inputContext,
                receivesInput,
                isKeyboardFocused,
                !UseFront2dCamera,
                !editorShellCapturesKeyboard,
                ActivateThisView,
                () => _editorRuntime.GameManager.ViewManager.ReleaseInput());
        }

        if (!HasWorldOverride)
        {
            UpdateGizmoInput(gameTime, inputContext, receivesInput, isKeyboardFocused);
        }
        else if (EnablePreviewGizmo)
        {
            UpdateGizmoInput(gameTime, inputContext, receivesInput, isKeyboardFocused);
        }
    }

    private bool IsBlockedByEditorModal()
    {
        var ownerWindow = _viewportHost?.SelfOrParentWindow;
        return ownerWindow != null && ownerWindow.Desktop.ActiveModalWindows.Count > 0;
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
        _selectedComponent = null;
        _selectedTransformable = entity?.RootComponent;
        _gizmoController.SetSelectedTransformable(_selectedTransformable, TryGetSelectionWorld());
    }

    public void SetSelectedComponent(EntityComponent? component)
    {
        _selectedComponent = component;

        if (component == null)
        {
            _selectedTransformable = _selectedEntity?.RootComponent;
            _gizmoController.SetSelectedTransformable(_selectedTransformable, TryGetSelectionWorld());
            return;
        }

        _selectedEntity = component.Owner;
        _selectedTransformable = component is SceneComponent sceneComponent
            ? sceneComponent
            : component.Owner?.RootComponent;

        _gizmoController.SetSelectedTransformable(_selectedTransformable, TryGetSelectionWorld());
    }

    public void SetSelectedTransformable(ITransformableObject? transformable)
    {
        _selectedTransformable = transformable;

        if (transformable is EntityComponent { Owner: { } owner })
        {
            _selectedEntity = owner;
        }

        _gizmoController.SetSelectedTransformable(transformable, TryGetSelectionWorld());
    }

    private void SelectPreviewEntityRoot()
    {
        if (_renderWorldOverride == null || _renderWorldOverride.Entities.Count == 0)
        {
            return;
        }

        var entity = _renderWorldOverride.Entities[0];
        if (ReferenceEquals(_selectedEntity, entity))
        {
            return;
        }

        _selectedEntity = entity;
        _selectedComponent = null;
        _selectedTransformable = entity.RootComponent;
        _gizmoController.SetSelectedTransformable(_selectedTransformable, _renderWorldOverride);
        SelectedEntityChanged?.Invoke(entity);
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
                _selectedComponent = null;
                _selectedTransformable = null;
                SelectedEntityChanged?.Invoke(null);
            }

            _gizmoController.SetSelectedTransformable(null, _renderWorldOverride);
            _cameraController.SetState(
                UseFront2dCamera ? 0f : MathHelper.PiOver4,
                0f - (UseFront2dCamera ? 0f : MathHelper.Pi / 6f),
                4.2f,
                Vector3.Zero);
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

        FocusBounds(entity.GetBoundingBox());
    }

    public void FocusBounds(BoundingBox bounds)
    {
        if (_camera == null)
        {
            return;
        }

        var focusTarget = (bounds.Min + bounds.Max) * 0.5f;

        if (UseFront2dCamera)
        {
            _front2dFocusedBounds = bounds;
            _front2dPixelsPerWorldUnit = _rtWidth > 16 && _rtHeight > 16
                ? ComputeFront2dFitPixelsPerWorldUnit(bounds)
                : 0f;
            ApplyFront2dFocus();
        }
        else
        {
            var diagonal = Vector3.Distance(bounds.Min, bounds.Max);
            var distance = Math.Max(5f, diagonal <= 0f ? 10f : diagonal * 1.5f);
            _cameraController.Focus(focusTarget, distance);
            _cameraController.ApplyTo(_camera);
        }
    }

    private float ComputeFront2dFitPixelsPerWorldUnit(BoundingBox bounds)
    {
        float width = Math.Max(1f, bounds.Max.X - bounds.Min.X);
        float height = Math.Max(1f, bounds.Max.Y - bounds.Min.Y);
        float availableWidth = Math.Max(1f, _rtWidth - 48f);
        float availableHeight = Math.Max(1f, _rtHeight - 48f);
        float fitPixelsPerWidth = availableWidth / width;
        float fitPixelsPerHeight = availableHeight / height;
        return Math.Clamp(Math.Min(fitPixelsPerWidth, fitPixelsPerHeight), MinFront2dPixelsPerWorldUnit, MaxFront2dPixelsPerWorldUnit);
    }

    private void ApplyFront2dFocus()
    {
        if (!UseFront2dCamera || _camera == null || !_front2dFocusedBounds.HasValue)
        {
            return;
        }

        if (_front2dPixelsPerWorldUnit <= 0f)
        {
            if (_rtWidth <= 16 || _rtHeight <= 16)
            {
                return;
            }

            _front2dPixelsPerWorldUnit = ComputeFront2dFitPixelsPerWorldUnit(_front2dFocusedBounds.Value);
        }

        BoundingBox bounds = _front2dFocusedBounds.Value;
        float verticalHalfFov = Math.Max(0.01f, _camera.FieldOfView * 0.5f);
        float halfDepth = Math.Max(0f, (bounds.Max.Z - bounds.Min.Z) * 0.5f);
        float distance = (_rtHeight * 0.5f) / (_front2dPixelsPerWorldUnit * MathF.Tan(verticalHalfFov));
        distance = Math.Clamp(distance + halfDepth + 8f, 0.5f, 1000f);

        Vector3 focusTarget = (bounds.Min + bounds.Max) * 0.5f;
        _cameraController.SetState(0f, 0f, distance, focusTarget);
        _cameraController.ApplyTo(_camera);
        _renderView?.Invalidate();
    }

    private void OnViewportScrolled(object? sender, BaseMouseScrolledEventArgs e)
    {
        if (!UseFront2dCamera || e.ScrollWheelDelta == 0 || !IsAttachedToWindow(_viewportHost) || !_front2dFocusedBounds.HasValue)
        {
            return;
        }

        Rectangle bounds = !_viewportHost.ActualLayoutBounds.IsEmpty ? _viewportHost.ActualLayoutBounds : _viewportHost.LayoutBounds;
        if (!bounds.Contains(e.Position))
        {
            return;
        }

        ActivateThisView(captureInput: false);
        if (_front2dPixelsPerWorldUnit <= 0f)
        {
            _front2dPixelsPerWorldUnit = ComputeFront2dFitPixelsPerWorldUnit(_front2dFocusedBounds.Value);
        }

        float wheelSteps = e.ScrollWheelDelta / 120.0f;
        _front2dPixelsPerWorldUnit = MathHelper.Clamp(
            _front2dPixelsPerWorldUnit * MathF.Pow(1.1f, wheelSteps),
            MinFront2dPixelsPerWorldUnit,
            MaxFront2dPixelsPerWorldUnit);

        ApplyFront2dFocus();
        e.SetHandledBy(_viewportHost ?? sender as IMouseHandlerHost);
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

        _surface?.RequestResize(width, height);
        _camera?.OnScreenResized(width, height);
        if (UseFront2dCamera)
        {
            ApplyFront2dFocus();
        }

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

        if (_selectedEntity != null && TryResolveSingleParticleAsset(draggedItems, out var selectedParticleAssetInfo))
        {
            DropParticleAssetOnEntity(_selectedEntity, selectedParticleAssetInfo);
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
            || string.Equals(extension, Constants.FileNameExtensions.Entity, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, Constants.FileNameExtensions.Particle, StringComparison.OrdinalIgnoreCase);
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

        if (string.Equals(item.Extension, Constants.FileNameExtensions.Particle, StringComparison.OrdinalIgnoreCase))
        {
            return CreateParticleEntity(assetInfo, dropIndex);
        }

        return CreateStaticModelEntity(assetInfo, dropIndex);
    }

    private Entity CreateParticleEntity(AssetInfo assetInfo, int dropIndex)
    {
        Vector3 spawnPosition = GetDropSpawnPosition(dropIndex);
        var entity = new Entity
        {
            Name = Path.GetFileNameWithoutExtension(assetInfo.FileName),
        };

        entity.RootComponent = EditorParticleSystemComponentService.CreateParticleComponent(assetInfo.Id, spawnPosition);
        return entity;
    }

    private bool TryResolveSingleParticleAsset(IReadOnlyList<ContentItem> draggedItems, out AssetInfo assetInfo)
    {
        assetInfo = null!;
        if (draggedItems.Count != 1)
        {
            return false;
        }

        ContentItem item = draggedItems[0];
        return string.Equals(item.Extension, Constants.FileNameExtensions.Particle, StringComparison.OrdinalIgnoreCase)
               && TryResolveDroppableAsset(item, out assetInfo);
    }

    private void DropParticleAssetOnEntity(Entity entity, AssetInfo assetInfo)
    {
        var existingComponent = entity.GetComponent<ParticleSystemComponent>();
        var previousSelection = _selectedEntity;

        if (existingComponent != null)
        {
            Guid previousAssetId = existingComponent.ParticleEffectAssetId;
            ExecuteWorldCommand(
                "Update Particle System Component",
                () =>
                {
                    EditorParticleSystemComponentService.ApplyParticleAsset(entity, existingComponent, assetInfo.Id);
                    ApplySelection(entity);
                },
                () =>
                {
                    EditorParticleSystemComponentService.ApplyParticleAsset(entity, existingComponent, previousAssetId);
                    ApplySelection(previousSelection);
                });
            return;
        }

        var component = EditorParticleSystemComponentService.CreateParticleComponent(assetInfo.Id, Vector3.Zero);
        var attachment = EditorParticleSystemComponentService.CreateAttachment(entity);
        ExecuteWorldCommand(
            "Add Particle System Component",
            () =>
            {
                EditorParticleSystemComponentService.AttachComponent(entity, component, attachment);
                ApplySelection(entity);
            },
            () =>
            {
                EditorParticleSystemComponentService.DetachComponent(entity, component, attachment);
                ApplySelection(previousSelection);
            });
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

    private void DeleteEntities(IReadOnlyList<Entity> entities)
    {
        var world = _renderView?.World ?? _editorRuntime.GameManager.CurrentWorld;
        if (world == null || entities.Count == 0)
        {
            return;
        }

        var deletions = new List<EntityDeletion>(entities.Count);
        for (int index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            deletions.Add(new EntityDeletion(entity, entity.Parent));
        }

        Entity? nextSelection = deletions.Count == 1 ? deletions[0].Parent : null;
        string description = deletions.Count == 1 ? "Delete Entity" : $"Delete {deletions.Count} Entities";

        ExecuteWorldCommand(
            description,
            () =>
            {
                for (int index = 0; index < deletions.Count; index++)
                {
                    EditorWorldEditingService.DetachEntity(world, deletions[index].Entity);
                }

                ApplySelection(nextSelection);
            },
            () =>
            {
                for (int index = 0; index < deletions.Count; index++)
                {
                    var deletion = deletions[index];
                    EditorWorldEditingService.AttachEntity(world, deletion.Entity, deletion.Parent);
                }

                ApplySelection(deletions.Count == 1 ? deletions[0].Entity : null);
            });
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
        _renderViewHost = new MguiViewportViewHost(renderView.Id, GetViewportScreenBounds);
        _renderView.Host = _renderViewHost;
        AttachWorld(world);
        if (ShowEditorOverlays)
        {
            EnsureEditorOverlays(world);
        }

        EnsureEditorGizmo(world);
        _gizmoController.SetSelectedTransformable(_selectedTransformable, world);
    }

    private void RegisterViewportInput()
    {
        if (_renderView == null)
        {
            return;
        }

        _editorRuntime.InputComponent.InputRouter?.RegisterViewInput(_renderView.Id, _windowInputSource);
    }

    private Rectangle GetViewportScreenBounds()
    {
        if (_viewportHost == null || !IsAttachedToWindow(_viewportHost))
        {
            return Rectangle.Empty;
        }

        return _viewportHost.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, _viewportHost.LayoutBounds);
    }

    private static bool IsAttachedToWindow(MGElement element)
    {
        MGElement current = element;
        while (current.Parent != null)
        {
            current = current.Parent;
        }

        return ReferenceEquals(current, current.SelfOrParentWindow);
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

    private MGElement BuildGizmoToolbar()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(6, 4, 6, 4),
        };

        _translateToggleButton = CreateGizmoToggleButton(EditorIcons.Move, "Move", isChecked =>
        {
            if (isChecked)
            {
                _gizmoController.ActiveMode = GizmoMode.Translate;
            }
            else
            {
                SynchronizeGizmoToolbarState();
            }
        });
        _rotateToggleButton = CreateGizmoToggleButton(EditorIcons.Rotate, "Rotate", isChecked =>
        {
            if (isChecked)
            {
                _gizmoController.ActiveMode = GizmoMode.Rotate;
            }
            else
            {
                SynchronizeGizmoToolbarState();
            }
        });
        _scaleToggleButton = CreateGizmoToggleButton(EditorIcons.Scale, "Scale", isChecked =>
        {
            if (isChecked)
            {
                _gizmoController.ActiveMode = GizmoMode.NonUniformScale;
            }
            else
            {
                SynchronizeGizmoToolbarState();
            }
        });
        _worldSpaceToggleButton = CreateGizmoToggleButton(EditorIcons.Globe, "World", isChecked =>
        {
            if (isChecked)
            {
                _gizmoController.ActiveSpace = TransformSpace.World;
            }
            else
            {
                SynchronizeGizmoToolbarState();
            }
        });
        _localSpaceToggleButton = CreateGizmoToggleButton(EditorIcons.Cuboid, "Local", isChecked =>
        {
            if (isChecked)
            {
                _gizmoController.ActiveSpace = TransformSpace.Local;
            }
            else
            {
                SynchronizeGizmoToolbarState();
            }
        });

        toolbar.TryAddChild(_translateToggleButton);
        toolbar.TryAddChild(_rotateToggleButton);
        toolbar.TryAddChild(_scaleToggleButton);
        toolbar.TryAddChild(new MGSeparator(_window, Orientation.Vertical)
        {
            Margin = new Thickness(4, 2, 4, 2),
        });
        toolbar.TryAddChild(_worldSpaceToggleButton);
        toolbar.TryAddChild(_localSpaceToggleButton);

        var surface = new MGBorder(
            _window,
            new Thickness(0, 0, 0, 1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PanelBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.ToolbarBackground)),
        };
        surface.SetContent(toolbar);
        return surface;
    }

    private MGToggleButton CreateGizmoToggleButton(Texture2D? icon, string fallbackLabel, Action<bool> onToggled)
    {
        var button = new MGToggleButton(_window)
        {
            Padding = new Thickness(4),
            MinWidth = 28,
            MinHeight = 28,
            PreferredWidth = 30,
            PreferredHeight = 30,
            BorderBrush = new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PanelBorder)),
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(new Color(34, 38, 46))),
            CheckedBackgroundBrush = new MGSolidFillBrush(new Color(58, 110, 182, 215)),
            CheckedTextForeground = Color.White,
        };

        if (icon != null)
        {
            button.SetContent(new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 18,
                PreferredHeight = 18,
            });
        }
        else
        {
            button.SetContent(new MGTextBlock(_window, fallbackLabel)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10,
            });
        }

        button.OnCheckStateChanged += (_, args) =>
        {
            if (_suspendToolbarCallbacks)
            {
                return;
            }

            onToggled(args.NewValue);
        };
        return button;
    }

    private void SynchronizeGizmoToolbarState()
    {
        if (!ShowGizmoToolbar)
        {
            return;
        }

        _suspendToolbarCallbacks = true;
        try
        {
            if (_translateToggleButton != null)
            {
                _translateToggleButton.IsChecked = _gizmoController.ActiveMode == GizmoMode.Translate;
            }

            if (_rotateToggleButton != null)
            {
                _rotateToggleButton.IsChecked = _gizmoController.ActiveMode == GizmoMode.Rotate;
            }

            if (_scaleToggleButton != null)
            {
                _scaleToggleButton.IsChecked = _gizmoController.ActiveMode == GizmoMode.NonUniformScale
                    || _gizmoController.ActiveMode == GizmoMode.UniformScale;
            }

            if (_worldSpaceToggleButton != null)
            {
                _worldSpaceToggleButton.IsChecked = _gizmoController.ActiveSpace == TransformSpace.World;
            }

            if (_localSpaceToggleButton != null)
            {
                _localSpaceToggleButton.IsChecked = _gizmoController.ActiveSpace == TransformSpace.Local;
            }
        }
        finally
        {
            _suspendToolbarCallbacks = false;
        }
    }

    public void ReleaseInputIfOutside(Point screenPosition)
    {
        if (_renderView == null || _viewportHost == null)
        {
            return;
        }

        if (_viewportHost.SelfOrParentWindow?.HasModalWindow == true)
        {
            _editorRuntime.GameManager.ViewManager.ReleaseInput();
            _editorRuntime.InputComponent.InputRouter?.ClearKeyboardFocus(_renderView.Id);
            return;
        }

        // Guard: treat detached (background-tab) viewport as if it has no bounds,
        // so clicks in the document area correctly release input / clear keyboard focus.
        if (GetViewportScreenBounds().Contains(screenPosition))
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
            _selectedComponent = null;
            _selectedTransformable = null;
            SelectedEntityChanged?.Invoke(null);
        }

        _gizmoController.SetSelectedTransformable(_selectedTransformable, desiredWorld);
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
            _selectedComponent = null;
            _selectedTransformable = null;
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
        _selectedComponent = null;
        _selectedTransformable = null;
        SelectedEntityChanged?.Invoke(null);

        if (sender is World world)
        {
            _gizmoController.RefreshWorldSelection(world, null);
        }
    }

    private void OnGizmoSelectedEntityChanged(Entity? entity)
    {
        _selectedEntity = entity;
        _selectedComponent = null;
        _selectedTransformable = entity?.RootComponent;
        SelectedEntityChanged?.Invoke(entity);
    }

    private void OnGizmoDeleteEntitiesRequested(IReadOnlyList<Entity> entities)
    {
        if (HasWorldOverride)
        {
            return;
        }

        DeleteEntities(entities);
    }

    private void OnGizmoActiveModeChanged(GizmoMode mode)
    {
        SynchronizeGizmoToolbarState();
    }

    private void OnGizmoActiveSpaceChanged(TransformSpace space)
    {
        SynchronizeGizmoToolbarState();
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
        _lightBillboardOverlayRenderer ??= new EditorLightBillboardOverlayRenderer(_graphicsDevice);
        _lightWireOverlayRenderer ??= new EditorLightWireOverlayRenderer(_editorRuntime.Content);
        _particleWireOverlayRenderer ??= new EditorParticleWireOverlayRenderer(_editorRuntime.Content);

        var overlayPipeline = _renderView.Pipeline as OverlayViewPipeline ?? new OverlayViewPipeline();
        overlayPipeline.RenderGridAction = (graphicsDevice, _, frame) => _grid?.DrawForView(graphicsDevice, in frame);
        overlayPipeline.RenderAxisAction = (graphicsDevice, _, frame) => _axis?.DrawForView(graphicsDevice, in frame);
        if (overlayPipeline.RenderVectorOverlayAction != RenderEditorVectorOverlay)
        {
            _externalVectorOverlayAction = overlayPipeline.RenderVectorOverlayAction;
            overlayPipeline.RenderVectorOverlayAction = RenderEditorVectorOverlay;
        }

        if (overlayPipeline.RenderUIOverlayAction != RenderEditorUiOverlay)
        {
            _externalUiOverlayAction = overlayPipeline.RenderUIOverlayAction;
            overlayPipeline.RenderUIOverlayAction = RenderEditorUiOverlay;
        }
        
        _renderView.Pipeline = overlayPipeline;
    }

    private void RenderEditorVectorOverlay(GraphicsDevice graphicsDevice, RenderView view, RenderFrame frame)
    {
        _externalVectorOverlayAction?.Invoke(graphicsDevice, view, frame);
        CaptureLightingDiagnostics(in frame);

        var lightItems = _lightOverlayCollector.Collect(view.World, _selectedEntity, _selectedComponent);
        _lightWireOverlayRenderer?.Draw(graphicsDevice, in frame, lightItems);

        var particleItems = _particleOverlayCollector.Collect(view.World, _selectedEntity, _selectedComponent);
        _particleWireOverlayRenderer?.Draw(graphicsDevice, in frame, particleItems);
    }

    private void RenderEditorUiOverlay(GraphicsDevice graphicsDevice, RenderView view, RenderFrame frame)
    {
        _externalUiOverlayAction?.Invoke(graphicsDevice, view, frame);
        CaptureLightingDiagnostics(in frame);

        var lightItems = _lightOverlayCollector.Collect(view.World, _selectedEntity, _selectedComponent);
        _lightBillboardOverlayRenderer?.Draw(graphicsDevice, in frame, lightItems);
    }

    private void CaptureLightingDiagnostics(in RenderFrame frame)
    {
        var lighting = frame.Lighting;
        if (lighting == null)
        {
            _lastActiveDirectionalLightCount = 0;
            _lastActivePointLightCount = 0;
            _lastActiveSpotLightCount = 0;
            return;
        }

        _lastActiveDirectionalLightCount = lighting.ActiveDirectionalLightCount;
        _lastActivePointLightCount = lighting.ActivePointLightCount;
        _lastActiveSpotLightCount = lighting.ActiveSpotLightCount;
    }

    private GridComponent CreateGridComponent()
    {
        var grid = new GridComponent(_editorRuntime);
        grid.Initialize();
        return grid;
    }

    private AxisComponent CreateAxisComponent()
    {
        var axis = new AxisComponent(_editorRuntime);
        axis.Initialize();
        return axis;
    }

    private void SynchronizeGizmo()
    {
        _gizmoController.AllowSelectionPicking = !HasWorldOverride;
        _gizmoController.AllowDeleteSelection = !HasWorldOverride;
        _gizmoController.Synchronize(_camera, _surface, _renderView?.World);
    }

    private void UpdateGizmoInput(GameTime gameTime, ViewInputContext inputContext, bool receivesInput, bool isKeyboardFocused)
    {
        _gizmoController.Update(gameTime, inputContext, receivesInput, isKeyboardFocused, _camera, _surface, _renderView?.World);
    }

    private bool ShouldSynchronizeGizmo()
    {
        return !HasWorldOverride || EnablePreviewGizmo;
    }

    private World? TryGetSelectionWorld()
    {
        return _renderView?.World ?? _renderWorldOverride ?? _editorRuntime.GameManager.CurrentWorld ?? _fallbackWorld;
    }

    public void Dispose()
    {
        DetachWorld();
        _gizmoController.DeleteEntitiesRequested -= OnGizmoDeleteEntitiesRequested;
        _gizmoController.SelectedEntityChanged -= OnGizmoSelectedEntityChanged;
        _gizmoController.ActiveModeChanged -= OnGizmoActiveModeChanged;
        _gizmoController.ActiveSpaceChanged -= OnGizmoActiveSpaceChanged;

        if (_viewportHost != null)
        {
            _viewportHost.MouseHandler.Scrolled -= OnViewportScrolled;
        }

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
        _lightBillboardOverlayRenderer?.Dispose();
        _lightBillboardOverlayRenderer = null;
        _lightWireOverlayRenderer?.Dispose();
        _lightWireOverlayRenderer = null;
        _particleWireOverlayRenderer?.Dispose();
        _particleWireOverlayRenderer = null;
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
