using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Rendering.Environment;
using CasaEngine.Editor.Styling;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering.Models;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

internal sealed class MaterialPreviewViewport : IDisposable
{
    private enum PreviewPrimitiveKind
    {
        Sphere,
        Cube,
        Plane,
    }

    private sealed class MguiPreviewViewHost : IViewHost, IViewScreenBoundsHost
    {
        private readonly Func<Rectangle> _getScreenBounds;
        private bool _disposed;

        public MguiPreviewViewHost(ViewId viewId, Func<Rectangle> getScreenBounds)
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
    private readonly MaterialCompiler _materialCompiler = new();
    private readonly Dictionary<PreviewPrimitiveKind, StaticModelMesh> _meshes = new();
    private readonly Dictionary<PreviewPrimitiveKind, MGButton> _shapeButtons = new();
    private readonly WorldEnvironmentSettings _environmentOverride = PreviewEnvironmentFactory.CreateNeutralPreview(EditorThemePalette.PreviewClearColor);
    private readonly PreviewWorldDriver _previewWorldDriver;

    private MGStackPanel? _root;
    private MGDockPanel? _viewportHost;
    private MGImage? _viewportImage;
    private MGTextBlock? _statusText;
    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiPreviewViewHost? _renderViewHost;
    private Texture2D? _boundTexture;
    private StaticModelSubMeshComponent? _previewMeshComponent;
    private Entity? _cameraEntity;
    private CameraLookAtComponent? _camera;
    private MaterialAsset? _materialAsset;
    private MaterialBase? _runtimeMaterial;
    private PreviewPrimitiveKind _activeShape = PreviewPrimitiveKind.Sphere;
    private string _statusMessage = "Preview ready. Neutral 3-point lighting.";
    private int _rtWidth = 240;
    private int _rtHeight = 220;
    private bool _disposed;

    public MaterialPreviewViewport(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "MaterialPreviewWorld",
        });
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderViewCreated();

        var header = new MGTextBlock(_window, "[b]Preview[/b]  [opacity=0.65]Neutral lighting[/opacity]")
        {
            Margin = new Thickness(4, 4, 4, 0),
            Opacity = EditorThemePalette.PrimaryHeaderOpacity,
        };

        var shapeRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(4, 0, 4, 0),
        };
        shapeRow.TryAddChild(CreateShapeButton(PreviewPrimitiveKind.Sphere, "Sphere"));
        shapeRow.TryAddChild(CreateShapeButton(PreviewPrimitiveKind.Cube, "Cube"));
        shapeRow.TryAddChild(CreateShapeButton(PreviewPrimitiveKind.Plane, "Plane"));

        _statusText = new MGTextBlock(_window, EscapeMarkup(_statusMessage))
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            FontSize = 10,
            WrapText = true,
        };

        _viewportHost = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 220,
            PreferredHeight = 220,
        };
        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;

        _viewportImage = new MGImage(_window, new MGTextureData(EditorIcons.AsImage(_surface!.Texture!)!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
        _viewportHost.TryAddChild(_viewportImage, Dock.Top);

        var viewportBorder = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground)),
            Margin = new Thickness(4, 0, 4, 4),
            Padding = new Thickness(1),
            MinHeight = 220,
            PreferredHeight = 220,
        };
        viewportBorder.SetContent(_viewportHost);

        if (_renderView != null && _renderView.Host != null)
        {
            _editorRuntime.GameManager.ViewManager.HookViewHost(_renderView);
        }

        _root = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(4, 0, 4, 6),
        };
        _root.TryAddChild(header);
        _root.TryAddChild(shapeRow);
        _root.TryAddChild(_statusText);
        _root.TryAddChild(viewportBorder);

        UpdateShapeButtons();
        RefreshTextureBinding();
        RefreshMaterial();
        return _root;
    }

    public World? GetOrCreatePreviewWorld()
    {
        EnsurePreviewSceneCreated();
        return _previewWorldDriver.World;
    }

    public void SetMaterialAsset(MaterialAsset? materialAsset)
    {
        _materialAsset = materialAsset;

        EnsurePreviewSceneCreated();

        RefreshMaterial();
    }

    public void RefreshMaterialAsset()
    {
        RefreshMaterial();
    }

    public void RefreshAfterDraw()
    {
        RefreshTextureBinding();
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(9)
        {
            $"Shape: {_activeShape}",
            $"Texture: {DescribeBoundTexture()}",
            $"View mode: {_renderView?.UpdateMode.ToString() ?? (_previewWorldDriver.World != null ? "ExternalWorldViewport" : "<none>")}",
            $"Status: {_statusMessage}",
            $"Preview world: {DescribePreviewWorld()}",
            $"Environment override active: {_renderView?.EnvironmentOverride != null}",
            $"Preview background mode: {_renderView?.EnvironmentOverride?.BackgroundMode.ToString() ?? "<world>"}",
            $"Physics isolated from main world: {DescribePhysicsIsolation()}",
            $"Physics debug world: {DescribeLastPhysicsDebugWorld()}",
        };

        int debugBodyCount = DescribeLastPhysicsDebugObjectCount();
        if (debugBodyCount >= 0)
        {
            result.Add($"Physics debug bodies: {debugBodyCount}");
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_viewportHost != null)
        {
            _viewportHost.OnLayoutBoundsChanged -= OnViewportBoundsChanged;
        }

        if (_renderView != null)
        {
            if (_renderView.Host != null)
            {
                _editorRuntime.GameManager.ViewManager.UnhookViewHost(_renderView);
                _renderView.Host = null;
            }

            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _renderViewHost?.Dispose();
        _renderViewHost = null;

        foreach (var mesh in _meshes.Values)
        {
            mesh.VertexBuffer?.Dispose();
            mesh.IndexBuffer?.Dispose();
        }

        _meshes.Clear();
        _surface?.Dispose();
        _surface = null;
        _previewWorldDriver.Dispose();

        _previewMeshComponent = null;
        _camera = null;
        _cameraEntity = null;
        _runtimeMaterial = null;
        _boundTexture = null;
    }

    private MGButton CreateShapeButton(PreviewPrimitiveKind shape, string label)
    {
        var button = new MGButton(_window, _ => SetShape(shape))
        {
            PreferredWidth = 62,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _shapeButtons.Add(shape, button);
        return button;
    }

    private void EnsureRenderViewCreated()
    {
        if (_renderView != null)
        {
            return;
        }

        EnsurePreviewSceneCreated();

        _cameraEntity = new Entity
        {
            Name = "MaterialPreviewCamera",
            IsVisible = false,
        };

        _camera = new CameraLookAtComponent();
        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(_previewWorldDriver.World!);
        ConfigureCamera();
        _camera.OnScreenResized(_rtWidth, _rtHeight);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _rtWidth,
            _rtHeight,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        var viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "Material Preview",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = EditorThemePalette.PreviewClearColor,
            EnvironmentOverride = _environmentOverride,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The material preview could not create its render view.");
        }

        _renderView = renderView;
        _renderViewHost = new MguiPreviewViewHost(renderView.Id,
            () => _viewportHost?.Parent != null ? _viewportHost.LayoutBounds : Rectangle.Empty);
        _renderView.Host = _renderViewHost;
        _renderView.Invalidate();
    }

    private void EnsurePreviewSceneCreated()
    {
        EnsurePreviewWorldCreated();
        ApplyShapeToPreview();
    }

    private void EnsurePreviewWorldCreated()
    {
        if (_previewWorldDriver.World != null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world =>
        {
            PreviewWorldLightRig.AddDefaultLights(world);

            var previewEntity = new Entity
            {
                Name = "MaterialPreviewEntity",
            };

            _previewMeshComponent = new StaticModelSubMeshComponent();
            previewEntity.RootComponent = _previewMeshComponent;
            world.AddEntity(previewEntity);
        });
    }

    private void SetShape(PreviewPrimitiveKind shape)
    {
        if (_activeShape == shape)
        {
            return;
        }

        _activeShape = shape;
        ApplyShapeToPreview();
    }

    private void ApplyShapeToPreview()
    {
        if (_previewMeshComponent == null)
        {
            return;
        }

        var mesh = GetOrCreateMesh(_activeShape);
        mesh.Material = _runtimeMaterial ?? StaticModelMaterialResolver.CreateMissingMaterial("Preview");

        _previewMeshComponent.ModelMesh = mesh;
        _previewMeshComponent.Position = Vector3.Zero;
        _previewMeshComponent.Scale = Vector3.One;
        _previewMeshComponent.Orientation = GetOrientation(_activeShape);

        // Materialize the queued preview entity and refresh its bounds after shape changes.
        _previewWorldDriver.RefreshNow();

        ConfigureCamera();
        UpdateShapeButtons();
        _renderView?.Invalidate();
    }

    private void ConfigureCamera()
    {
        if (_camera == null)
        {
            return;
        }

        Vector3 position = _activeShape switch
        {
            PreviewPrimitiveKind.Plane => new Vector3(0.0f, 0.0f, 3.0f),
            PreviewPrimitiveKind.Cube => new Vector3(0.0f, 0.4f, 3.8f),
            _ => new Vector3(0.0f, 0.3f, 3.4f),
        };
        Vector3 target = _activeShape switch
        {
            PreviewPrimitiveKind.Cube => new Vector3(0.0f, 0.2f, 0.0f),
            _ => Vector3.Zero,
        };

        _camera.SetPositionAndTarget(position, target);
    }

    private Quaternion GetOrientation(PreviewPrimitiveKind shape)
    {
        return shape switch
        {
            PreviewPrimitiveKind.Cube => Quaternion.CreateFromYawPitchRoll(-MathHelper.PiOver4, MathHelper.PiOver4 * 0.5f, 0.0f),
            PreviewPrimitiveKind.Plane => Quaternion.CreateFromYawPitchRoll(0.0f, 0.0f, 0.0f) * Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.PiOver2),
            _ => Quaternion.Identity,
        };
    }

    private StaticModelMesh GetOrCreateMesh(PreviewPrimitiveKind shape)
    {
        if (_meshes.TryGetValue(shape, out var mesh))
        {
            return mesh;
        }

        GeometricPrimitive primitive = shape switch
        {
            PreviewPrimitiveKind.Cube => new BoxPrimitive(1.8f, 1.8f, 1.8f),
            PreviewPrimitiveKind.Plane => new PlanePrimitive(2.6f, 2.6f),
            _ => new SpherePrimitive(1.8f, 24),
        };

        mesh = new StaticModelMesh
        {
            Name = $"{shape}PreviewMesh",
            SlotName = "Preview",
            MaterialSlotIndex = 0,
        };
        mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
        mesh.Initialize(_graphicsDevice);
        _meshes.Add(shape, mesh);
        return mesh;
    }

    private void RefreshMaterial()
    {
        if (_materialAsset == null)
        {
            _runtimeMaterial = StaticModelMaterialResolver.CreateMissingMaterial("Preview");
            SetStatusMessage("No material loaded.");
            ApplyMaterialToMeshes();
            return;
        }

        try
        {
            _runtimeMaterial = _materialCompiler.CompileRuntimeMaterial(_materialAsset, _editorRuntime.AssetContentManager);
            SetStatusMessage("Preview ready. Neutral 3-point lighting.");
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            _runtimeMaterial = StaticModelMaterialResolver.CreateMissingMaterial("Preview");
            SetStatusMessage($"Preview fallback: {exception.Message}");
        }

        ApplyMaterialToMeshes();
    }

    private void ApplyMaterialToMeshes()
    {
        foreach (var mesh in _meshes.Values)
        {
            mesh.Material = _runtimeMaterial;
        }

        if (_previewMeshComponent?.ModelMesh != null)
        {
            _previewMeshComponent.ModelMesh.Material = _runtimeMaterial;
        }

        _renderView?.Invalidate();
    }

    private void OnViewportBoundsChanged(object? sender, EventArgs<Rectangle> e)
    {
        int width = Math.Max(32, e.NewValue.Width);
        int height = Math.Max(32, e.NewValue.Height);

        if (width == _rtWidth && height == _rtHeight)
        {
            return;
        }

        _rtWidth = width;
        _rtHeight = height;
        _renderViewHost?.NotifyResized(width, height);
        _surface?.RequestResize(width, height);
        _camera?.OnScreenResized(width, height);
        _renderView?.Invalidate();
        RefreshTextureBinding();
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

    private void UpdateShapeButtons()
    {
        foreach (var pair in _shapeButtons)
        {
            pair.Value.IsEnabled = pair.Key != _activeShape;
        }
    }

    private void SetStatusMessage(string message)
    {
        _statusMessage = message;
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(message);
        }
    }

    private string DescribeBoundTexture()
    {
        return _boundTexture == null
            ? "<none>"
            : $"{_boundTexture.Width}x{_boundTexture.Height}";
    }

    private string DescribePreviewWorld()
    {
        return _previewWorldDriver.World?.Name ?? "<none>";
    }

    private string DescribePhysicsIsolation()
    {
        if (_previewWorldDriver.World == null || _editorRuntime.GameManager.CurrentWorld == null)
        {
            return "<n/a>";
        }

        return (!ReferenceEquals(_previewWorldDriver.World.PhysicsWorld, _editorRuntime.GameManager.CurrentWorld.PhysicsWorld)).ToString();
    }

    private string DescribeLastPhysicsDebugWorld()
    {
        if (_renderView == null)
        {
            return _previewWorldDriver.World?.Name ?? "<none>";
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

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}