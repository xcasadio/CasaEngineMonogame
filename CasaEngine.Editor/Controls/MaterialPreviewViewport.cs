using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Core.Log;
using CasaEngine.Editor.Runtime;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.World;
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

    private MGStackPanel? _root;
    private MGDockPanel? _viewportHost;
    private MGImage? _viewportImage;
    private MGTextBlock? _statusText;
    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiPreviewViewHost? _renderViewHost;
    private Texture2D? _boundTexture;
    private World? _previewWorld;
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
            Opacity = 0.9f,
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
            Opacity = 0.72f,
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

        _viewportImage = new MGImage(_window, new MGTextureData(_surface!.Texture!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
        _viewportHost.TryAddChild(_viewportImage, Dock.Top);

        var viewportBorder = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(new Color(74, 74, 82))))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(new Color(18, 18, 22))),
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

    public void SetMaterialAsset(MaterialAsset? materialAsset)
    {
        _materialAsset = materialAsset;

        if (_renderView == null)
        {
            return;
        }

        RefreshMaterial();
    }

    public void RefreshAfterDraw()
    {
        RefreshTextureBinding();
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(4)
        {
            $"Shape: {_activeShape}",
            $"Texture: {DescribeBoundTexture()}",
            $"View mode: {_renderView?.UpdateMode.ToString() ?? "<none>"}",
            $"Status: {_statusMessage}",
        };

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

        if (_previewWorld != null)
        {
            _previewWorld.Clear();
            _previewWorld = null;
        }

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

        EnsurePreviewWorldCreated();

        _cameraEntity = new Entity
        {
            Name = "MaterialPreviewCamera",
            IsVisible = false,
        };

        _camera = new CameraLookAtComponent();
        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(_previewWorld!);
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
            World = _previewWorld!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = new Color(20, 22, 28),
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

        ApplyShapeToPreview();
        _renderView.Invalidate();
    }

    private void EnsurePreviewWorldCreated()
    {
        if (_previewWorld != null)
        {
            return;
        }

        _previewWorld = new World
        {
            Name = "MaterialPreviewWorld",
        };
        _previewWorld.LoadContent(_editorRuntime);

        var previewEntity = new Entity
        {
            Name = "MaterialPreviewEntity",
        };

        _previewMeshComponent = new StaticModelSubMeshComponent();
        previewEntity.RootComponent = _previewMeshComponent;
        _previewWorld.AddEntity(previewEntity);
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
        _previewMeshComponent.LocalPosition = Vector3.Zero;
        _previewMeshComponent.LocalScale = Vector3.One;
        _previewMeshComponent.LocalOrientation = GetOrientation(_activeShape);

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
        _surface?.EnsureSize(width, height);
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
            _viewportImage.Source = new MGTextureData(texture);
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

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}