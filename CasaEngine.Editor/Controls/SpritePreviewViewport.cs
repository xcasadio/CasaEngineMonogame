using System;
using System.Collections.Generic;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Overlays;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

internal sealed class SpritePreviewViewport : IDisposable
{
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
    private readonly PreviewWorldDriver _previewWorldDriver;
    private readonly EditorSpriteOverlayRenderer _overlayRenderer;

    private MGStackPanel _root;
    private MGDockPanel _viewportHost;
    private MGImage _viewportImage;
    private MGTextBlock _statusText;
    private MGTextBlock _zoomText;
    private MGCheckBox _showCollisionsCheckBox;
    private MGCheckBox _showHotspotCheckBox;
    private RenderTargetSurface _surface;
    private RenderView _renderView;
    private MguiPreviewViewHost _renderViewHost;
    private Texture2D _boundTexture;
    private Entity _previewEntity;
    private EditorSpritePreviewComponent _previewSpriteComponent;
    private Entity _cameraEntity;
    private CameraLookAtComponent _camera;
    private SpriteData _spriteData;
    private string _statusMessage = "Open a .sprite asset from the Content Browser.";
    private int _rtWidth = 360;
    private int _rtHeight = 260;
    private bool _showCollisions = true;
    private bool _showHotspot = true;
    private bool _fitZoom = true;
    private float _pixelsPerUnit = 1f;
    private bool _suspendControlCallbacks;
    private bool _disposed;

    private const float MinPixelsPerUnit = 0.05f;
    private const float MaxPixelsPerUnit = 4096f;

    public SpritePreviewViewport(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _overlayRenderer = new EditorSpriteOverlayRenderer();
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "SpritePreviewWorld",
            UpdateMode = PreviewWorldUpdateMode.Manual,
        });
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderViewCreated();

        _statusText = new MGTextBlock(_window, EscapeMarkup(_statusMessage))
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            FontSize = 10,
            WrapText = true,
        };

        _zoomText = new MGTextBlock(_window, "Zoom: 100%")
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 8,
            Margin = new Thickness(4, 0, 4, 0),
        };
        _showCollisionsCheckBox = CreateCheckBox("Collisions", _showCollisions, isChecked =>
        {
            _showCollisions = isChecked == true;
            _renderView?.Invalidate();
        });
        _showHotspotCheckBox = CreateCheckBox("Hotspot", _showHotspot, isChecked =>
        {
            _showHotspot = isChecked == true;
            _renderView?.Invalidate();
        });
        toolbar.TryAddChild(_showCollisionsCheckBox);
        toolbar.TryAddChild(_showHotspotCheckBox);
        toolbar.TryAddChild(_zoomText);
        toolbar.TryAddChild(CreateButton("100%", SetZoom100));
        toolbar.TryAddChild(CreateButton("Fit", FitToView));

        _viewportHost = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 260,
        };
        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;
        _viewportHost.OnEndingDraw += OnViewportEndingDraw;
        _viewportHost.MouseHandler.Scrolled += OnViewportScrolled;

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
            MinHeight = 260,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        _root.TryAddChild(toolbar);
        _root.TryAddChild(_statusText);
        _root.TryAddChild(viewportBorder);

        SynchronizeControlsFromState();
        RefreshZoomText();
        RefreshTextureBinding();
        return _root;
    }

    public void LoadAsset(SpriteData spriteData)
    {
        ArgumentNullException.ThrowIfNull(spriteData);

        _spriteData = spriteData;
        EnsurePreviewSceneCreated();
        _previewEntity!.Name = string.IsNullOrWhiteSpace(spriteData.Name) ? "Sprite Preview" : spriteData.Name;
        _previewSpriteComponent!.SetSpriteData(spriteData);
        _previewWorldDriver.RefreshNow();
        FitToView();
        SetStatusMessage($"Preview ready. Sprite size: {spriteData.PositionInTexture.Width} x {spriteData.PositionInTexture.Height}.");
        _renderView?.Invalidate();
    }

    public void ClearAsset()
    {
        _spriteData = null;
        _previewSpriteComponent?.SetSpriteData(null);
        _fitZoom = true;
        _pixelsPerUnit = 1f;
        SetStatusMessage("Open a .sprite asset from the Content Browser.");
        RefreshZoomText();
        _renderView?.Invalidate();
    }

    public void RefreshAfterDraw()
    {
        RefreshTextureBinding();
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(8)
        {
            $"View world: {_renderView?.World.Name ?? "<none>"}",
            $"Texture: {DescribeBoundTexture()}",
            $"Status: {_statusMessage}",
            $"Zoom percent: {MathF.Round(_pixelsPerUnit * 100f, 2)}",
            $"Fit zoom: {_fitZoom}",
            $"Show collisions: {_showCollisions}",
            $"Show hotspot: {_showHotspot}",
        };

        var previewComponentStates = _previewSpriteComponent?.GetDebugStateSnapshot() ?? Array.Empty<string>();
        for (int index = 0; index < previewComponentStates.Count; index++)
        {
            result.Add(previewComponentStates[index]);
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
            _viewportHost.OnEndingDraw -= OnViewportEndingDraw;
            _viewportHost.MouseHandler.Scrolled -= OnViewportScrolled;
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
        _overlayRenderer.Dispose();
        _surface?.Dispose();
        _surface = null;
        _previewWorldDriver.Dispose();
        _previewSpriteComponent = null;
        _camera = null;
        _cameraEntity = null;
        _boundTexture = null;
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
            Name = "SpritePreviewCamera",
            IsVisible = false,
        };

        _camera = new CameraLookAtComponent();
        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(_previewWorldDriver.World!);
        _camera.OnScreenResized(_rtWidth, _rtHeight);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _rtWidth,
            _rtHeight,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        var viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "Sprite Preview",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = EditorThemePalette.PreviewClearColor,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The sprite preview could not create its render view.");
        }

        _renderView = renderView;
        _renderViewHost = new MguiPreviewViewHost(renderView.Id, GetViewportScreenBounds);
        _renderView.Host = _renderViewHost;
        _renderView.Invalidate();
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

    private void EnsurePreviewSceneCreated()
    {
        if (_previewWorldDriver.World != null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world =>
        {
            _previewEntity = new Entity
            {
                Name = "SpritePreviewEntity",
            };

            _previewSpriteComponent = new EditorSpritePreviewComponent();
            _previewEntity.RootComponent = _previewSpriteComponent;
            world.AddEntity(_previewEntity);
        });
    }

    private void ConfigureCamera()
    {
        if (_camera == null || _spriteData == null)
        {
            return;
        }

        BoundingBox bounds = SpriteDataBoundsCalculator.CalculateLocalBounds(_spriteData);
        float verticalHalfFov = Math.Max(0.01f, _camera.FieldOfView * 0.5f);
        float halfDepth = Math.Max(0f, (bounds.Max.Z - bounds.Min.Z) * 0.5f);
        float distance = (_rtHeight * 0.5f) / (_pixelsPerUnit * MathF.Tan(verticalHalfFov));
        distance = Math.Clamp(distance + halfDepth + 2f, 0.5f, 1000f);

        Vector3 focusTarget = (bounds.Min + bounds.Max) * 0.5f;
        _camera.SetPositionAndTarget(new Vector3(focusTarget.X, focusTarget.Y, distance), focusTarget);
        _renderView?.Invalidate();
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
        if (_fitZoom && _spriteData != null)
        {
            _pixelsPerUnit = ComputeFitPixelsPerUnit(_spriteData);
            RefreshZoomText();
        }

        ConfigureCamera();
        RefreshTextureBinding();
    }

    private void OnViewportScrolled(object sender, MGUI.Shared.Input.Mouse.BaseMouseScrolledEventArgs e)
    {
        if (_spriteData == null || e.ScrollWheelDelta == 0 || _viewportHost == null || _viewportHost.Parent == null)
        {
            return;
        }

        Rectangle bounds = !_viewportHost.ActualLayoutBounds.IsEmpty ? _viewportHost.ActualLayoutBounds : _viewportHost.LayoutBounds;
        if (!bounds.Contains(e.Position))
        {
            return;
        }

        float wheelSteps = e.ScrollWheelDelta / 120.0f;
        _fitZoom = false;
        _pixelsPerUnit = MathHelper.Clamp(_pixelsPerUnit * MathF.Pow(1.1f, wheelSteps), MinPixelsPerUnit, MaxPixelsPerUnit);
        RefreshZoomText();
        ConfigureCamera();
        e.SetHandledBy(_viewportHost ?? sender as IMouseHandlerHost);
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

    private void FitToView()
    {
        if (_spriteData == null)
        {
            return;
        }

        _fitZoom = true;
        _pixelsPerUnit = ComputeFitPixelsPerUnit(_spriteData);
        RefreshZoomText();
        ConfigureCamera();
    }

    private void SetZoom100()
    {
        _fitZoom = false;
        _pixelsPerUnit = 1f;
        RefreshZoomText();
        ConfigureCamera();
    }

    private float ComputeFitPixelsPerUnit(SpriteData spriteData)
    {
        BoundingBox bounds = SpriteDataBoundsCalculator.CalculateLocalBounds(spriteData);
        float width = Math.Max(1f, bounds.Max.X - bounds.Min.X);
        float height = Math.Max(1f, bounds.Max.Y - bounds.Min.Y);
        float availableWidth = Math.Max(1f, _rtWidth - 16f);
        float availableHeight = Math.Max(1f, _rtHeight - 16f);
        return Math.Clamp(Math.Min(availableWidth / width, availableHeight / height), MinPixelsPerUnit, MaxPixelsPerUnit);
    }

    private void RefreshZoomText()
    {
        if (_zoomText == null)
        {
            return;
        }

        _zoomText.Text = $"Zoom: {_pixelsPerUnit * 100f:0.#}%";
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 64,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private MGCheckBox CreateCheckBox(string label, bool isChecked, Action<bool?> onChanged)
    {
        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = isChecked,
        };
        checkBox.SetContent(new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        checkBox.OnCheckStateChanged += (_, args) =>
        {
            if (_suspendControlCallbacks)
            {
                return;
            }

            onChanged(args.NewValue);
        };
        return checkBox;
    }

    private void SynchronizeControlsFromState()
    {
        _suspendControlCallbacks = true;
        try
        {
            if (_showCollisionsCheckBox != null)
            {
                _showCollisionsCheckBox.IsChecked = _showCollisions;
            }

            if (_showHotspotCheckBox != null)
            {
                _showHotspotCheckBox.IsChecked = _showHotspot;
            }
        }
        finally
        {
            _suspendControlCallbacks = false;
        }
    }

    private void OnViewportEndingDraw(object sender, MGElement.MGElementDrawEventArgs e)
    {
        if (_previewSpriteComponent == null || _spriteData == null || _viewportHost == null)
        {
            return;
        }

        Rectangle viewportBounds = !_viewportHost.ActualLayoutBounds.IsEmpty
            ? _viewportHost.ActualLayoutBounds
            : _viewportHost.LayoutBounds;
        if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
        {
            return;
        }

        _overlayRenderer.Draw(
            e.DA.Context,
            viewportBounds,
            _spriteData,
            _previewSpriteComponent.Position,
            new Vector2(_previewSpriteComponent.Scale.X, _previewSpriteComponent.Scale.Y),
            _pixelsPerUnit,
            _showCollisions,
            _showHotspot,
            e.DA.Opacity);
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}