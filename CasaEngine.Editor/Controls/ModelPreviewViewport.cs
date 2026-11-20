using System;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Rendering.Environment;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A 3D view of one <see cref="StaticModel"/> on neutral preview lighting, rendered into a render target and shown in an
/// <see cref="MGImage"/>. Reusable by any document that shows a static model: <see cref="ShowModel"/> displays a given model
/// (an in-memory one is enough), <see cref="Clear"/> empties the view and <see cref="Dispose"/> releases the view, the
/// render target and the preview world.
/// </summary>
/// <remarks>
/// The viewport does not own the model: the caller releases the model's own resources. The model is scaled and centred so
/// its bounding box fits a fixed size, and the camera looks at it from a fixed three-quarter angle; the lights of the preview
/// rig are therefore always sized for the model whatever its units. Modeled on <see cref="MaterialPreviewViewport"/>.
/// </remarks>
internal sealed class ModelPreviewViewport : IDisposable
{
    /// <summary>Largest side of the framed bounding box, in preview-world units (the light rig is sized for about 2).</summary>
    private const float FramedExtent = 2.0f;

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

        public event Action<IViewHost, int, int> Resized;

        public event Action<IViewHost> Closed;

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
    private readonly int _minHeight;
    private readonly WorldEnvironmentSettings _environmentOverride = PreviewEnvironmentFactory.CreateNeutralPreview(EditorThemePalette.PreviewClearColor);
    private readonly PreviewWorldDriver _previewWorldDriver;

    private MGDockPanel _viewportHost;
    private MGImage _viewportImage;
    private MGBorder _root;
    private RenderTargetSurface _surface;
    private RenderView _renderView;
    private MguiPreviewViewHost _renderViewHost;
    private Texture2D _boundTexture;
    private Entity _modelEntity;
    private StaticModelComponent _modelComponent;
    private Entity _cameraEntity;
    private PerspectiveCameraComponent _camera;
    private int _rtWidth = 320;
    private int _rtHeight = 240;
    private bool _disposed;

    /// <summary>Creates the viewport; the render target and the preview world are created by <see cref="CreateContent"/>.</summary>
    /// <param name="minHeight">Minimum height of the view, in pixels.</param>
    public ModelPreviewViewport(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, int minHeight = 220)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(editorRuntime);

        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _minHeight = minHeight;
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "ModelPreviewWorld",
        });
    }

    /// <summary>The model currently shown, or null.</summary>
    public StaticModel Model { get; private set; }

    /// <summary>Builds the MGUI content (a bordered image fed by the render target). Returns the same element on later calls.</summary>
    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderViewCreated();

        _viewportHost = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = _minHeight,
        };
        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;

        _viewportImage = new MGImage(_window, new MGTextureData(EditorIcons.AsImage(_surface!.Texture!)!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
        _viewportHost.TryAddChild(_viewportImage, Dock.Top);

        _root = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground)),
            Margin = new Thickness(4, 0, 4, 4),
            Padding = new Thickness(1),
            MinHeight = _minHeight,
        };
        _root.SetContent(_viewportHost);

        if (_renderView != null && _renderView.Host != null)
        {
            _editorRuntime.GameManager.ViewManager.HookViewHost(_renderView);
        }

        _boundTexture = _surface.Texture;
        return _root;
    }

    /// <summary>
    /// Shows <paramref name="model"/>: replaces the previous one, scales and centres it to the framed extent and invalidates
    /// the view. A model without any mesh leaves the view empty. The model is initialised by the world with the editor's asset
    /// content manager; it is not disposed by this viewport.
    /// </summary>
    public void ShowModel(StaticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureRenderViewCreated();
        RemoveModelEntity();
        Model = model;
        AssignNeutralMaterials(model);

        _modelComponent = new StaticModelComponent
        {
            StaticModel = model,
        };
        _modelEntity = new Entity
        {
            Name = "ModelPreviewEntity",
            RootComponent = _modelComponent,
        };
        _previewWorldDriver.World!.AddEntity(_modelEntity);

        // First refresh materialises the entity and builds the sub-mesh components, which gives the bounds.
        _previewWorldDriver.RefreshNow();
        FrameModel();
        _previewWorldDriver.RefreshNow();
        _renderView?.Invalidate();
    }

    /// <summary>Removes the shown model; the view renders the empty preview world.</summary>
    public void Clear()
    {
        RemoveModelEntity();
        Model = null;
        _renderView?.Invalidate();
    }

    /// <summary>Re-binds the render target texture to the image when the surface recreated it. Call once per frame after the views were drawn.</summary>
    public void RefreshAfterDraw()
    {
        RefreshTextureBinding();
    }

    /// <summary>Releases the render view, the render target and the preview world.</summary>
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

        _surface?.Dispose();
        _surface = null;
        _previewWorldDriver.Dispose();

        _modelEntity = null;
        _modelComponent = null;
        _camera = null;
        _cameraEntity = null;
        _boundTexture = null;
        Model = null;
    }

    /// <summary>
    /// Gives a neutral gray material to every mesh that names no material asset and holds no runtime material (a model read
    /// straight from a glTF file): without it the world would resolve such a mesh to the magenta missing-material fallback.
    /// A mesh that does name a material asset is left alone, so a model of the project shows its own materials.
    /// </summary>
    private static void AssignNeutralMaterials(StaticModel model)
    {
        LitDiffuseMaterial neutral = null;
        foreach (StaticModelMesh mesh in model.Meshes)
        {
            if (mesh.MaterialAssetId != Guid.Empty || mesh.Material != null)
            {
                continue;
            }

            neutral ??= new LitDiffuseMaterial
            {
                Name = "Preview Neutral",
                DiffuseColor = new Color(205, 205, 212),
                EmissiveColor = Vector3.Zero,
                SpecularColor = new Vector3(0.35f),
                SpecularPower = 24.0f,
            };
            mesh.Material = neutral;
        }
    }

    private void RemoveModelEntity()
    {
        if (_modelEntity == null)
        {
            return;
        }

        _previewWorldDriver.World?.RemoveEntity(_modelEntity);
        _previewWorldDriver.RefreshNow();
        _modelEntity = null;
        _modelComponent = null;
    }

    private void FrameModel()
    {
        BoundingBox bounds = _modelComponent.GetBoundingBox();
        Vector3 size = bounds.Max - bounds.Min;
        float largestSide = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (!float.IsFinite(largestSide) || largestSide <= 1e-6f)
        {
            Logs.WriteWarning($"Model preview: '{Model?.Name}' has no usable bounding box, it is shown as is.");
            return;
        }

        // The root component sits at the origin with an identity transform, so its bounds are in the same space as its
        // position: scaling by s moves the centre to centre * s, which the position then cancels.
        float scale = FramedExtent / largestSide;
        Vector3 center = (bounds.Min + bounds.Max) * 0.5f;
        _modelComponent.Scale = new Vector3(scale);
        _modelComponent.Position = -center * scale;
    }

    private void EnsureRenderViewCreated()
    {
        if (_renderView != null)
        {
            return;
        }

        EnsurePreviewWorldCreated();

        _camera = new PerspectiveCameraComponent();
        _cameraEntity = EditorCameraFactory.CreateCameraEntity("ModelPreviewCamera", _camera, _previewWorldDriver.World!);
        _camera.NearPlane = 0.05f;
        _camera.SetPositionAndTarget(new Vector3(2.4f, 1.5f, 3.0f), Vector3.Zero);
        _camera.OnScreenResized(_rtWidth, _rtHeight);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _rtWidth,
            _rtHeight,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        var viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "Model Preview",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = EditorThemePalette.PreviewClearColor,
            EnvironmentOverride = _environmentOverride,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The model preview could not create its render view.");
        }

        _renderView = renderView;
        _renderViewHost = new MguiPreviewViewHost(renderView.Id, GetViewportScreenBounds);
        _renderView.Host = _renderViewHost;
        _renderView.Invalidate();
    }

    private void EnsurePreviewWorldCreated()
    {
        if (_previewWorldDriver.World != null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world => PreviewWorldLightRig.AddDefaultLights(world));
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

    private void OnViewportBoundsChanged(object sender, EventArgs<Rectangle> e)
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
}
