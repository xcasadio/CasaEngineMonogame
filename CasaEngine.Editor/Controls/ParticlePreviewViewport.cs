using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Overlays;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
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

internal sealed class ParticlePreviewViewport : IDisposable
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
    private readonly WorldEnvironmentSettings _environmentOverride = PreviewEnvironmentFactory.CreateNeutralPreview(EditorThemePalette.PreviewClearColor);
    private readonly PreviewWorldDriver _previewWorldDriver;
    private readonly EditorParticleOverlayCollector _particleOverlayCollector = new();

    private MGStackPanel? _root;
    private MGTextBlock? _titleText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGTextBlock? _metricsText;
    private MGDockPanel? _viewportHost;
    private MGImage? _viewportImage;
    private MGButton? _playButton;
    private MGButton? _pauseButton;
    private MGButton? _stopButton;
    private MGButton? _restartButton;
    private MGCheckBox? _loopCheckBox;
    private MGSlider? _speedSlider;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiPreviewViewHost? _renderViewHost;
    private Texture2D? _boundTexture;
    private Entity? _previewEntity;
    private ParticleSystemComponent? _particleComponent;
    private EditorParticleWireOverlayRenderer? _particleWireOverlayRenderer;
    private Entity? _cameraEntity;
    private CameraLookAtComponent? _camera;

    private ParticleEffectAsset? _particleAsset;
    private string? _loadedRelativePath;
    private bool _isPlaying = true;
    private bool _isLooping = true;
    private float _simulationSpeed = 1.0f;
    private string _statusMessage = "Open a .particle asset from the Content Browser.";
    private int _lastAliveCount;
    private int _lastDeadCount;
    private int _lastEmittedCount;
    private int _lastKilledCount;
    private int _lastMaxAliveCountReached;
    private bool _lastMaxReached;
    private double _lastSimulationCpuMilliseconds;
    private double _lastExtractionCpuMilliseconds;
    private double _lastRenderCpuMilliseconds;
    private int _lastDrawCallCount;
    private int _rtWidth = 360;
    private int _rtHeight = 260;
    private bool _disposed;
    private bool _suspendControlCallbacks;

    public ParticlePreviewViewport(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "ParticlePreviewWorld",
            UpdateMode = PreviewWorldUpdateMode.Continuous,
        });
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderViewCreated();

        _titleText = new MGTextBlock(_window, "[b]Particle Preview[/b]")
        {
            Margin = new Thickness(4, 4, 4, 0),
            Opacity = EditorThemePalette.PrimaryHeaderOpacity,
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No particle asset loaded.")
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryHeadingOpacity,
            WrapText = true,
        };

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(4, 0, 4, 0),
        };
        _playButton = CreateButton("Play", PlayPreview);
        _pauseButton = CreateButton("Pause", PausePreview);
        _stopButton = CreateButton("Stop", StopPreview);
        _restartButton = CreateButton("Restart", RestartPreview);
        var resetCameraButton = CreateButton("Reset Camera", ResetCamera);
        toolbar.TryAddChild(_playButton);
        toolbar.TryAddChild(_pauseButton);
        toolbar.TryAddChild(_stopButton);
        toolbar.TryAddChild(_restartButton);
        toolbar.TryAddChild(resetCameraButton);

        var optionsRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 8,
            Margin = new Thickness(4, 0, 4, 0),
        };
        _loopCheckBox = CreateCheckBox("Loop", _isLooping, isChecked =>
        {
            _isLooping = isChecked == true;
            ApplyRuntimeConfiguration(restart: true);
        });
        optionsRow.TryAddChild(_loopCheckBox);

        var speedRow = CreateSliderRow("Sim Speed", 0.0f, 3.0f, _simulationSpeed, "F2", out _speedSlider, value =>
        {
            _simulationSpeed = value;
            if (_particleComponent != null)
            {
                _particleComponent.SimulationSpeed = value;
            }

            RefreshMetricsText();
        });

        _statusText = new MGTextBlock(_window, EscapeMarkup(_statusMessage))
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            FontSize = 10,
            WrapText = true,
        };

        _metricsText = new MGTextBlock(_window, "Alive: 0  Emitted: 0  Draw Calls: 0")
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
            MinHeight = 260,
            PreferredHeight = 260,
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
            MinHeight = 260,
            PreferredHeight = 260,
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
        _root.TryAddChild(_titleText);
        _root.TryAddChild(_sourceText);
        _root.TryAddChild(toolbar);
        _root.TryAddChild(optionsRow);
        _root.TryAddChild(speedRow);
        _root.TryAddChild(_statusText);
        _root.TryAddChild(_metricsText);
        _root.TryAddChild(viewportBorder);

        RefreshHeaderText();
        SynchronizeControlsFromState();
        RefreshMetricsText();
        RefreshTextureBinding();
        return _root;
    }

    public World? GetOrCreatePreviewWorld()
    {
        EnsurePreviewSceneCreated();
        return _previewWorldDriver.World;
    }

    public void LoadAsset(ParticleEffectAsset particleAsset, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(particleAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _particleAsset = particleAsset;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        _isPlaying = true;
        _isLooping = HasLoopingEmitter(particleAsset);
        _simulationSpeed = 1.0f;
        EnsurePreviewSceneCreated();
        ApplyRuntimeConfiguration(restart: true);
        RefreshHeaderText();
        SynchronizeControlsFromState();
        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    public void RefreshParticleAsset()
    {
        ApplyRuntimeConfiguration(restart: true);
    }

    public void Update(GameTime gameTime)
    {
        if (_previewWorldDriver.World == null || _particleComponent == null)
        {
            return;
        }

        _previewWorldDriver.Tick(gameTime);
        var runtimeInstance = _particleComponent.RuntimeInstance;
        var metrics = runtimeInstance?.Metrics ?? ParticleRuntimeMetrics.Empty;
        _lastAliveCount = metrics.AliveCount;
        _lastDeadCount = metrics.DeadCount;
        _lastEmittedCount = metrics.LastEmittedCount;
        _lastKilledCount = metrics.LastKilledCount;
        _lastMaxAliveCountReached = metrics.MaxAliveCountReached;
        _lastMaxReached = metrics.MaxReached;
        _lastSimulationCpuMilliseconds = metrics.SimulationCpuMilliseconds;
        _lastExtractionCpuMilliseconds = _particleComponent.LastExtractionCpuMilliseconds;
        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    public void RefreshPreviewAfterDraw()
    {
        _lastDrawCallCount = _editorRuntime.ParticleRendererComponent?.FrameDrawCallCount ?? 0;
        _lastRenderCpuMilliseconds = _editorRuntime.ParticleRendererComponent?.FrameFlushCpuMilliseconds ?? 0.0;
        RefreshMetricsText();
        RefreshTextureBinding();
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(10)
        {
            $"Asset: {_particleAsset?.Name ?? "<none>"}",
            $"Path: {_loadedRelativePath ?? "<none>"}",
            $"Playback: {DescribePlaybackState()}",
            $"Loop: {_isLooping}",
            $"Simulation speed: {_simulationSpeed:0.###}",
            $"Alive particles: {_lastAliveCount}",
            $"Dead particles: {_lastDeadCount}",
            $"Last emitted: {_lastEmittedCount}",
            $"Last killed: {_lastKilledCount}",
            $"Max alive reached: {_lastMaxAliveCountReached}",
            $"Max reached: {_lastMaxReached}",
            $"Simulation CPU ms: {_lastSimulationCpuMilliseconds:0.###}",
            $"Extraction CPU ms: {_lastExtractionCpuMilliseconds:0.###}",
            $"Render CPU ms: {_lastRenderCpuMilliseconds:0.###}",
            $"Draw calls: {_lastDrawCallCount}",
            $"Texture: {DescribeBoundTexture()}",
            $"World: {_previewWorldDriver.World?.Name ?? "<none>"}",
            $"Particle gizmos: {_particleWireOverlayRenderer?.LastDrawnItemCount ?? 0}",
            $"Particle gizmo lines: {_particleWireOverlayRenderer?.LastDrawnLineCount ?? 0}",
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
        _particleWireOverlayRenderer?.Dispose();
        _particleWireOverlayRenderer = null;
        _surface?.Dispose();
        _surface = null;
        _previewWorldDriver.Dispose();

        _camera = null;
        _cameraEntity = null;
        _previewEntity = null;
        _particleComponent = null;
        _boundTexture = null;
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 90,
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

    private MGElement CreateSliderRow(
        string label,
        float minimum,
        float maximum,
        float currentValue,
        string valueFormat,
        out MGSlider slider,
        Action<float> onValueChanged)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 8,
            Margin = new Thickness(4, 0, 4, 0),
        };

        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 92,
            VerticalAlignment = VerticalAlignment.Center,
        });

        slider = new MGSlider(_window, minimum, maximum, currentValue)
        {
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ShowValueLabel = true,
            ValueLabelFormat = valueFormat,
        };
        slider.ValueChanged += (_, args) =>
        {
            if (_suspendControlCallbacks)
            {
                return;
            }

            onValueChanged(args.NewValue);
        };
        row.TryAddChild(slider);
        return row;
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
            Name = "ParticlePreviewCamera",
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
            Name = "Particle Preview",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = EditorThemePalette.PreviewClearColor,
            EnvironmentOverride = _environmentOverride,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The particle preview could not create its render view.");
        }

        _renderView = renderView;
        _particleWireOverlayRenderer ??= new EditorParticleWireOverlayRenderer(_editorRuntime.Content);
        var overlayPipeline = renderView.Pipeline as OverlayViewPipeline ?? new OverlayViewPipeline();
        overlayPipeline.RenderVectorOverlayAction = RenderPreviewVectorOverlay;
        renderView.Pipeline = overlayPipeline;
        _renderViewHost = new MguiPreviewViewHost(renderView.Id,
            () => _viewportHost?.Parent != null ? _viewportHost.LayoutBounds : Rectangle.Empty);
        _renderView.Host = _renderViewHost;
        _renderView.Invalidate();
    }

    private void RenderPreviewVectorOverlay(GraphicsDevice graphicsDevice, RenderView view, RenderFrame frame)
    {
        var particleItems = _particleOverlayCollector.Collect(view.World, _previewEntity, _particleComponent);
        _particleWireOverlayRenderer?.Draw(graphicsDevice, in frame, particleItems);
    }

    private void EnsurePreviewSceneCreated()
    {
        if (_previewWorldDriver.World != null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world =>
        {
            PreviewWorldLightRig.AddDefaultLights(world);

            _previewEntity = new Entity
            {
                Name = "ParticlePreviewEntity",
            };

            _particleComponent = new ParticleSystemComponent
            {
                PlayOnStart = false,
                Looping = _isLooping,
                SimulateInEditor = true,
                SimulationSpeed = _simulationSpeed,
                ColorTint = Color.White,
            };
            _previewEntity.RootComponent = _particleComponent;
            world.AddEntity(_previewEntity);
        });

        ResetPreviewTransform();
    }

    private void ApplyRuntimeConfiguration(bool restart)
    {
        if (_particleComponent == null)
        {
            return;
        }

        _particleComponent.Looping = _isLooping;
        _particleComponent.SimulationSpeed = _simulationSpeed;

        if (_particleAsset == null)
        {
            _particleComponent.Stop(clearParticles: true);
            SetStatusMessage("No particle asset loaded.");
            RefreshMetricsText();
            return;
        }

        _particleComponent.SetParticleEffectAsset(_particleAsset);
        if (restart)
        {
            _particleComponent.Restart(clearParticles: true);
            _isPlaying = true;
        }
        else if (_isPlaying)
        {
            _particleComponent.Play();
        }
        else
        {
            _particleComponent.Pause();
        }

        SetStatusMessage($"Preview ready. Emitters: {_particleAsset.Emitters.Count}.");
        UpdatePlaybackButtons();
        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    private void PlayPreview()
    {
        _isPlaying = true;
        _particleComponent?.Play();
        SetStatusMessage("Preview playing.");
        UpdatePlaybackButtons();
        _renderView?.Invalidate();
    }

    private void PausePreview()
    {
        _isPlaying = false;
        _particleComponent?.Pause();
        SetStatusMessage("Preview paused.");
        UpdatePlaybackButtons();
        _renderView?.Invalidate();
    }

    private void StopPreview()
    {
        _isPlaying = false;
        _particleComponent?.Stop(clearParticles: true);
        _lastAliveCount = 0;
        _lastDeadCount = _particleComponent?.RuntimeInstance?.Metrics.DeadCount ?? 0;
        _lastEmittedCount = 0;
        _lastKilledCount = 0;
        _lastMaxAliveCountReached = 0;
        _lastMaxReached = false;
        _lastSimulationCpuMilliseconds = 0.0;
        _lastExtractionCpuMilliseconds = 0.0;
        _lastRenderCpuMilliseconds = 0.0;
        SetStatusMessage("Preview stopped.");
        UpdatePlaybackButtons();
        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    private void RestartPreview()
    {
        _isPlaying = true;
        _particleComponent?.Restart(clearParticles: true);
        SetStatusMessage("Preview restarted.");
        UpdatePlaybackButtons();
        _renderView?.Invalidate();
    }

    private void ResetCamera()
    {
        ResetPreviewTransform();
        _renderView?.Invalidate();
    }

    private void ResetPreviewTransform()
    {
        if (_particleComponent != null)
        {
            _particleComponent.LocalPosition = Vector3.Zero;
            _particleComponent.LocalOrientation = Quaternion.Identity;
            _particleComponent.LocalScale = Vector3.One;
        }

        ConfigureCamera();
    }

    private void ConfigureCamera()
    {
        if (_camera == null)
        {
            return;
        }

        float radius = CalculatePreviewRadius();
        Vector3 target = new(0.0f, radius * 0.25f, 0.0f);
        Vector3 position = new(0.0f, radius * 0.45f, Math.Max(3.0f, radius * 3.0f));
        _camera.SetPositionAndTarget(position, target);
    }

    private float CalculatePreviewRadius()
    {
        if (_particleAsset == null || _particleAsset.Emitters.Count == 0)
        {
            return 1.5f;
        }

        float radius = 1.5f;
        for (int emitterIndex = 0; emitterIndex < _particleAsset.Emitters.Count; emitterIndex++)
        {
            var emitter = _particleAsset.Emitters[emitterIndex];
            radius = MathF.Max(radius, emitter.Shape.Radius + MathF.Max(emitter.Shape.Size.X, emitter.Shape.Size.Y) * 0.5f);
            radius = MathF.Max(radius, MathF.Max(emitter.Initial.Size.Max.X, emitter.Initial.Size.Max.Y));
            radius = MathF.Max(radius, emitter.Initial.Speed.Max * emitter.Initial.Lifetime.Max * 0.35f);
        }

        return MathHelper.Clamp(radius, 1.5f, 12.0f);
    }

    private void RefreshHeaderText()
    {
        if (_titleText != null)
        {
            string title = _particleAsset?.Name;
            _titleText.Text = string.IsNullOrWhiteSpace(title)
                ? "[b]Particle Preview[/b]"
                : $"[b]Particle Preview[/b]  [opacity=0.7]{EscapeMarkup(title)}[/opacity]";
        }

        if (_sourceText != null)
        {
            _sourceText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
                ? "No particle asset loaded."
                : EscapeMarkup(_loadedRelativePath);
        }
    }

    private void RefreshMetricsText()
    {
        if (_metricsText == null)
        {
            return;
        }

        _metricsText.Text = $"Alive: {_lastAliveCount}  Dead: {_lastDeadCount}  Emit/Kill: {_lastEmittedCount}/{_lastKilledCount}  Max: {_lastMaxAliveCountReached}{(_lastMaxReached ? "!" : string.Empty)}  CPU S/E/R: {_lastSimulationCpuMilliseconds:0.##}/{_lastExtractionCpuMilliseconds:0.##}/{_lastRenderCpuMilliseconds:0.##} ms  Draws: {_lastDrawCallCount}  State: {DescribePlaybackState()}  Speed: {_simulationSpeed:0.##}x";
    }

    private void SynchronizeControlsFromState()
    {
        _suspendControlCallbacks = true;
        try
        {
            if (_loopCheckBox != null)
            {
                _loopCheckBox.IsChecked = _isLooping;
            }

            if (_speedSlider != null)
            {
                _speedSlider.Value = _simulationSpeed;
            }
        }
        finally
        {
            _suspendControlCallbacks = false;
        }

        UpdatePlaybackButtons();
    }

    private void UpdatePlaybackButtons()
    {
        bool hasAsset = _particleAsset != null;
        if (_playButton != null)
        {
            _playButton.IsEnabled = hasAsset && !_isPlaying;
        }

        if (_pauseButton != null)
        {
            _pauseButton.IsEnabled = hasAsset && _isPlaying;
        }

        if (_stopButton != null)
        {
            _stopButton.IsEnabled = hasAsset;
        }

        if (_restartButton != null)
        {
            _restartButton.IsEnabled = hasAsset;
        }
    }

    private void OnViewportBoundsChanged(object? sender, EventArgs<Rectangle> e)
    {
        int width = Math.Max(64, e.NewValue.Width);
        int height = Math.Max(64, e.NewValue.Height);
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
            _viewportImage.Source = new MGTextureData(EditorIcons.AsImage(texture)!);
        }
    }

    private string DescribePlaybackState()
    {
        ParticlePlaybackState playbackState = _particleComponent?.RuntimeInstance?.PlaybackState ?? ParticlePlaybackState.Stopped;
        return playbackState.ToString();
    }

    private string DescribeBoundTexture()
    {
        return _boundTexture == null
            ? "<none>"
            : $"{_boundTexture.Width}x{_boundTexture.Height}";
    }

    private void SetStatusMessage(string message)
    {
        _statusMessage = message;
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(message);
        }
    }

    private static bool HasLoopingEmitter(ParticleEffectAsset particleAsset)
    {
        for (int emitterIndex = 0; emitterIndex < particleAsset.Emitters.Count; emitterIndex++)
        {
            if (particleAsset.Emitters[emitterIndex].Looping)
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}