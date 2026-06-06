using System;
using System.IO;

using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Rendering.Environment;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

internal sealed class AnimationClipPreviewPanel : IDisposable
{
    private enum PreviewMode
    {
        Clip,
        Blend,
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
    private readonly WorldEnvironmentSettings _environmentOverride = PreviewEnvironmentFactory.CreateNeutralPreview(EditorThemePalette.PreviewClearColor);
    private readonly PreviewWorldDriver _previewWorldDriver;

    private MGStackPanel? _root;
    private MGTextBlock? _titleText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGTextBlock? _metricsText;
    private MGDockPanel? _viewportHost;
    private MGImage? _viewportImage;
    private MGButton? _playPauseButton;
    private MGButton? _modeButton;
    private MGCheckBox? _loopCheckBox;
    private MGCheckBox? _rootMotionApplyCheckBox;
    private MGSlider? _speedSlider;
    private MGSlider? _blendWeightSlider;

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private MguiPreviewViewHost? _renderViewHost;
    private Texture2D? _boundTexture;
    private Entity? _previewEntity;
    private SkinnedMeshComponent? _skinnedMeshComponent;
    private Entity? _cameraEntity;
    private CameraLookAtComponent? _camera;

    private AnimationClipAsset? _animationClipAsset;
    private string? _loadedRelativePath;
    private AssetInfo? _resolvedPreviewMeshAssetInfo;
    private AnimationClip? _selectedClip;
    private AnimationClip? _blendReferenceClip;
    private AnimationClipNode? _selectedClipNode;
    private AnimationClipNode? _blendReferenceClipNode;
    private LinearBlendAnimationNode? _linearBlendNode;
    private PreviewMode _previewMode;
    private bool _isPlaying = true;
    private bool _isLooping = true;
    private bool _applyRootMotion;
    private float _playbackSpeed = 1f;
    private float _blendWeight = 0.5f;
    private float _lastRootMotionMagnitude;
    private string _lastEventName = string.Empty;
    private string _lastMetricsText = string.Empty;
    private string _statusMessage = "Open a .skeletonAnim asset from the Content Browser.";
    private int _rtWidth = 320;
    private int _rtHeight = 280;
    private bool _disposed;
    private bool _suspendControlCallbacks;

    public AnimationClipPreviewPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "AnimationClipPreviewWorld",
            UpdateMode = PreviewWorldUpdateMode.Continuous,
        });
    }

    public string? LoadedRelativePath => _loadedRelativePath;

    public AnimationClipAsset? LoadedAnimationClipAsset => _animationClipAsset;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderViewCreated();

        _titleText = new MGTextBlock(_window, "[b]Animation Clip Preview[/b]")
        {
            Margin = new Thickness(4, 4, 4, 0),
            Opacity = EditorThemePalette.PrimaryHeaderOpacity,
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No animation clip loaded.")
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
        _playPauseButton = CreateButton("Pause", TogglePlayback);
        _modeButton = CreateButton("Mode: Clip", TogglePreviewMode);
        var resetButton = CreateButton("Reset", ResetPreviewPose);
        toolbar.TryAddChild(_playPauseButton);
        toolbar.TryAddChild(_modeButton);
        toolbar.TryAddChild(resetButton);

        var optionsRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 8,
            Margin = new Thickness(4, 0, 4, 0),
        };
        _loopCheckBox = CreateCheckBox("Loop", _isLooping, isChecked =>
        {
            _isLooping = isChecked == true;
            ApplyPreviewPlaybackConfiguration(resetTime: false);
        });
        _rootMotionApplyCheckBox = CreateCheckBox("Apply Root Motion", _applyRootMotion, isChecked =>
        {
            _applyRootMotion = isChecked == true;
            ApplyPreviewPlaybackConfiguration(resetTime: true);
            ResetPreviewTransform();
        });
        optionsRow.TryAddChild(_loopCheckBox);
        optionsRow.TryAddChild(_rootMotionApplyCheckBox);

        var speedRow = CreateSliderRow(
            "Speed",
            0.1f,
            2.5f,
            _playbackSpeed,
            "F2",
            out _speedSlider,
            value =>
            {
                _playbackSpeed = value;
                ApplyPreviewPlaybackConfiguration(resetTime: false);
            });

        var blendRow = CreateSliderRow(
            "Blend Weight",
            0f,
            1f,
            _blendWeight,
            "F2",
            out _blendWeightSlider,
            value =>
            {
                _blendWeight = value;
                if (_linearBlendNode != null)
                {
                    _linearBlendNode.Weight = value;
                }

                _renderView?.Invalidate();
            });

        _statusText = new MGTextBlock(_window, EscapeMarkup(_statusMessage))
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            FontSize = 10,
            WrapText = true,
        };

        _metricsText = new MGTextBlock(_window, "Duration: --")
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            FontSize = 10,
            WrapText = true,
            MinLines = 1,
            HasStableTextFootprint = true,
        };

        _viewportHost = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 280,
            PreferredHeight = 280,
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
            MinHeight = 280,
            PreferredHeight = 280,
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
        _root.TryAddChild(blendRow);
        _root.TryAddChild(_statusText);
        _root.TryAddChild(_metricsText);
        _root.TryAddChild(viewportBorder);

        UpdateModeButton();
        UpdatePlayPauseButton();
        UpdateBlendControlsEnabledState();
        RefreshMetricsText();
        RefreshTextureBinding();
        return _root;
    }

    public void LoadAsset(AnimationClipAsset animationClipAsset, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(animationClipAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _animationClipAsset = animationClipAsset;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        _resolvedPreviewMeshAssetInfo = null;
        _selectedClip = null;
        _blendReferenceClip = null;
        _selectedClipNode = null;
        _blendReferenceClipNode = null;
        _linearBlendNode = null;
        _previewMode = PreviewMode.Clip;
        _isPlaying = true;
        _isLooping = true;
        _applyRootMotion = false;
        _playbackSpeed = 1f;
        _blendWeight = 0.5f;
        _lastRootMotionMagnitude = 0f;
        _lastEventName = string.Empty;

        EnsurePreviewSceneCreated();
        ClearPreviewMesh();

        try
        {
            Guid clipAssetId = animationClipAsset.AssetId != Guid.Empty ? animationClipAsset.AssetId : animationClipAsset.Id;
            _selectedClip = _editorRuntime.AssetContentManager.Load<AnimationClip>(clipAssetId);
            _resolvedPreviewMeshAssetInfo = ResolvePreviewMeshAsset(animationClipAsset);
            if (_resolvedPreviewMeshAssetInfo == null)
            {
                SetStatusMessage("No compatible skinned mesh asset was found for this clip skeleton.");
            }
            else
            {
                LoadCompatibleSkinnedMesh(_resolvedPreviewMeshAssetInfo);
                _blendReferenceClip = ResolveBlendReferenceClip(clipAssetId);
                SetStatusMessage(_blendReferenceClip != null
                    ? $"Preview mesh: {_resolvedPreviewMeshAssetInfo.Name}. Blend partner: {_blendReferenceClip.Name}."
                    : $"Preview mesh: {_resolvedPreviewMeshAssetInfo.Name}. No secondary clip available for blend preview.");
            }
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            _selectedClip = null;
            _blendReferenceClip = null;
            SetStatusMessage($"Preview load failed: {exception.Message}");
        }

        ApplyPreviewPlaybackConfiguration(resetTime: true);
        ResetPreviewTransform();
        RefreshHeaderText();
        SynchronizeControlsFromState();
        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    public void Update(GameTime gameTime)
    {
        if (_previewWorldDriver.World == null || _skinnedMeshComponent == null)
        {
            return;
        }

        _previewWorldDriver.Tick(gameTime);

        var rootMotionDelta = _skinnedMeshComponent.ConsumeRootMotionDelta();
        _lastRootMotionMagnitude = rootMotionDelta.Translation.Length();
        if (_applyRootMotion)
        {
            _skinnedMeshComponent.LocalPosition += rootMotionDelta.Translation * 0.1f;
            _skinnedMeshComponent.LocalOrientation = Quaternion.Normalize(_skinnedMeshComponent.LocalOrientation * rootMotionDelta.Rotation);
        }

        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    public void RefreshPreviewAfterDraw()
    {
        RefreshTextureBinding();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_skinnedMeshComponent != null)
        {
            _skinnedMeshComponent.AnimationEventTriggered -= OnAnimationEventTriggered;
        }

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

        _camera = null;
        _cameraEntity = null;
        _previewEntity = null;
        _skinnedMeshComponent = null;
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
            Name = "AnimationClipPreviewCamera",
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
            Name = "Animation Clip Preview",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = EditorThemePalette.PreviewClearColor,
            EnvironmentOverride = _environmentOverride,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out var renderView))
        {
            throw new InvalidOperationException("The animation clip preview could not create its render view.");
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
            PreviewWorldLightRig.AddDefaultLights(world);

            _previewEntity = new Entity
            {
                Name = "AnimationClipPreviewEntity",
            };

            _skinnedMeshComponent = new SkinnedMeshComponent();
            _skinnedMeshComponent.AnimationEventTriggered += OnAnimationEventTriggered;
            _previewEntity.RootComponent = _skinnedMeshComponent;
            world.AddEntity(_previewEntity);
        });
        ResetPreviewTransform();
    }

    private void LoadCompatibleSkinnedMesh(AssetInfo previewMeshAssetInfo)
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        var skinnedMesh = _editorRuntime.AssetContentManager.Load<SkinnedMesh>(previewMeshAssetInfo.Id);
        skinnedMesh.Initialize(_editorRuntime.AssetContentManager);
        _skinnedMeshComponent.SkinnedMesh = skinnedMesh;
    }

    private void ClearPreviewMesh()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        _skinnedMeshComponent.ClearTwoBoneIkConstraints();
        _skinnedMeshComponent.StopAnimation();
        _skinnedMeshComponent.SkinnedMesh = null;
    }

    private AssetInfo? ResolvePreviewMeshAsset(AnimationClipAsset animationClipAsset)
    {
        AssetInfo? bestMatch = null;
        Guid currentClipId = animationClipAsset.AssetId != Guid.Empty ? animationClipAsset.AssetId : animationClipAsset.Id;

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            if (!assetInfo.FileName.EndsWith(Constants.FileNameExtensions.Model, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string fullPath = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
            if (!TryLoadSkinnedMeshAsset(fullPath, out var skinnedMeshAsset))
            {
                continue;
            }

            if (skinnedMeshAsset.SkeletonAssetId != animationClipAsset.SkeletonAssetId)
            {
                continue;
            }

            if (skinnedMeshAsset.DefaultAnimationClipAssetId == currentClipId)
            {
                return assetInfo;
            }

            for (int clipIndex = 0; clipIndex < skinnedMeshAsset.AnimationClipAssetIds.Count; clipIndex++)
            {
                if (skinnedMeshAsset.AnimationClipAssetIds[clipIndex] == currentClipId)
                {
                    return assetInfo;
                }
            }

            bestMatch ??= assetInfo;
        }

        return bestMatch;
    }

    private AnimationClip? ResolveBlendReferenceClip(Guid selectedClipAssetId)
    {
        if (_skinnedMeshComponent?.SkinnedMesh == null)
        {
            return null;
        }

        var skinnedMesh = _skinnedMeshComponent.SkinnedMesh;
        if (skinnedMesh.DefaultAnimationClipAssetId != Guid.Empty && skinnedMesh.DefaultAnimationClipAssetId != selectedClipAssetId)
        {
            return _editorRuntime.AssetContentManager.Load<AnimationClip>(skinnedMesh.DefaultAnimationClipAssetId);
        }

        for (int clipIndex = 0; clipIndex < skinnedMesh.AnimationClipAssetIds.Count; clipIndex++)
        {
            Guid clipAssetId = skinnedMesh.AnimationClipAssetIds[clipIndex];
            if (clipAssetId == selectedClipAssetId)
            {
                continue;
            }

            return _editorRuntime.AssetContentManager.Load<AnimationClip>(clipAssetId);
        }

        return null;
    }

    private void TogglePlayback()
    {
        _isPlaying = !_isPlaying;
        if (_skinnedMeshComponent?.SkinnedMesh?.RiggedModel == null)
        {
            UpdatePlayPauseButton();
            return;
        }

        if (_isPlaying)
        {
            _skinnedMeshComponent.ResumeAnimation();
        }
        else
        {
            _skinnedMeshComponent.PauseAnimation();
        }

        UpdatePlayPauseButton();
        _renderView?.Invalidate();
    }

    private void TogglePreviewMode()
    {
        if (_previewMode == PreviewMode.Clip)
        {
            if (_blendReferenceClip == null)
            {
                SetStatusMessage("Blend preview unavailable because no second clip was found on the compatible mesh.");
                return;
            }

            _previewMode = PreviewMode.Blend;
        }
        else
        {
            _previewMode = PreviewMode.Clip;
        }

        ApplyPreviewPlaybackConfiguration(resetTime: true);
        ResetPreviewTransform();
        UpdateModeButton();
        UpdateBlendControlsEnabledState();
    }

    private void ApplyPreviewPlaybackConfiguration(bool resetTime)
    {
        var controller = _skinnedMeshComponent?.SkinnedMesh?.RiggedModel?.AnimationController;
        if (controller == null || _selectedClip == null)
        {
            UpdateModeButton();
            UpdatePlayPauseButton();
            UpdateBlendControlsEnabledState();
            return;
        }

        controller.RootMotionMode = _applyRootMotion ? RootMotionMode.Apply : RootMotionMode.Observe;

        if (_previewMode == PreviewMode.Blend && _blendReferenceClip != null)
        {
            _selectedClipNode ??= new AnimationClipNode(_selectedClip, _isLooping);
            _blendReferenceClipNode ??= new AnimationClipNode(_blendReferenceClip, _isLooping);
            _linearBlendNode ??= new LinearBlendAnimationNode(_blendReferenceClipNode, _selectedClipNode, _blendWeight);

            _selectedClipNode.Loop = _isLooping;
            _selectedClipNode.Speed = _playbackSpeed;
            _blendReferenceClipNode.Loop = _isLooping;
            _blendReferenceClipNode.Speed = _playbackSpeed;
            _linearBlendNode.Weight = _blendWeight;

            if (resetTime)
            {
                _selectedClipNode.TimeSeconds = 0f;
                _blendReferenceClipNode.TimeSeconds = 0f;
            }

            if (resetTime || !ReferenceEquals(controller.GraphRoot, _linearBlendNode))
            {
                _skinnedMeshComponent!.PlayAnimationGraph(_linearBlendNode);
            }
        }
        else
        {
            if (resetTime || controller.CurrentState == null || !ReferenceEquals(controller.CurrentState.Clip, _selectedClip))
            {
                controller.Play(_selectedClip, _isLooping, _playbackSpeed);
            }

            if (controller.CurrentState != null)
            {
                controller.CurrentState.Loop = _isLooping;
                controller.CurrentState.Speed = _playbackSpeed;
                if (resetTime)
                {
                    controller.CurrentState.Seek(0f);
                }
            }
        }

        if (_isPlaying)
        {
            _skinnedMeshComponent!.ResumeAnimation();
        }
        else
        {
            _skinnedMeshComponent!.PauseAnimation();
        }

        UpdateModeButton();
        UpdatePlayPauseButton();
        UpdateBlendControlsEnabledState();
        _renderView?.Invalidate();
    }

    private void ResetPreviewPose()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        ApplyPreviewPlaybackConfiguration(resetTime: true);
        ResetPreviewTransform();
        _previewWorldDriver.RefreshNow();
        RefreshMetricsText();
        _renderView?.Invalidate();
    }

    private void ResetPreviewTransform()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        _skinnedMeshComponent.LocalPosition = Vector3.Zero;
        _skinnedMeshComponent.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        _skinnedMeshComponent.LocalScale = new Vector3(0.1f);
        ConfigureCamera();
    }

    private void ConfigureCamera()
    {
        if (_camera == null)
        {
            return;
        }

        _camera.SetPositionAndTarget(new Vector3(0f, 1.35f, 6.8f), new Vector3(0f, 1.1f, 0f));
    }

    private void RefreshHeaderText()
    {
        if (_titleText != null)
        {
            string title = _animationClipAsset?.Name;
            _titleText.Text = string.IsNullOrWhiteSpace(title)
                ? "[b]Animation Clip Preview[/b]"
                : $"[b]Animation Clip Preview[/b]  [opacity=0.7]{EscapeMarkup(title)}[/opacity]";
        }

        if (_sourceText != null)
        {
            string meshLabel = _resolvedPreviewMeshAssetInfo?.Name ?? "<no compatible mesh>";
            string clipSource = string.IsNullOrWhiteSpace(_loadedRelativePath) ? "No asset loaded." : EscapeMarkup(_loadedRelativePath);
            _sourceText.Text = $"{clipSource}\nPreview mesh: {EscapeMarkup(meshLabel)}";
        }
    }

    private void RefreshMetricsText()
    {
        if (_metricsText == null)
        {
            return;
        }

        float duration = _selectedClip?.DurationSeconds ?? 0f;
        var controller = _skinnedMeshComponent?.SkinnedMesh?.RiggedModel?.AnimationController;
        float currentTime = controller?.CurrentTimeSeconds ?? 0f;
        string lastEvent = string.IsNullOrWhiteSpace(_lastEventName) ? "<none>" : _lastEventName;
        string metricsText =
            $"Duration: {duration:F2}s  Time: {currentTime:F2}s  Root Motion Δ: {_lastRootMotionMagnitude:F3}  Last Event: {EscapeMarkup(lastEvent)}";
        if (string.Equals(metricsText, _lastMetricsText, StringComparison.Ordinal))
        {
            return;
        }

        _lastMetricsText = metricsText;
        _metricsText.SetText(metricsText, MGTextInvalidationMode.ReflowLocal);
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

            if (_rootMotionApplyCheckBox != null)
            {
                _rootMotionApplyCheckBox.IsChecked = _applyRootMotion;
            }

            if (_speedSlider != null)
            {
                _speedSlider.Value = _playbackSpeed;
            }

            if (_blendWeightSlider != null)
            {
                _blendWeightSlider.Value = _blendWeight;
            }
        }
        finally
        {
            _suspendControlCallbacks = false;
        }
    }

    private void UpdateModeButton()
    {
        if (_modeButton == null)
        {
            return;
        }

        _modeButton.SetContent(new MGTextBlock(_window, _previewMode == PreviewMode.Blend ? "Mode: Blend" : "Mode: Clip")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _modeButton.IsEnabled = _selectedClip != null && (_blendReferenceClip != null || _previewMode == PreviewMode.Blend);
    }

    private void UpdatePlayPauseButton()
    {
        if (_playPauseButton == null)
        {
            return;
        }

        _playPauseButton.SetContent(new MGTextBlock(_window, _isPlaying ? "Pause" : "Play")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _playPauseButton.IsEnabled = _selectedClip != null;
    }

    private void UpdateBlendControlsEnabledState()
    {
        if (_blendWeightSlider != null)
        {
            _blendWeightSlider.IsEnabled = _previewMode == PreviewMode.Blend && _blendReferenceClip != null;
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

    private void OnAnimationEventTriggered(AnimationEventKeyframe eventKeyframe)
    {
        _lastEventName = eventKeyframe.EventName;
        RefreshMetricsText();
    }

    private void SetStatusMessage(string message)
    {
        _statusMessage = message;
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(message);
        }
    }

    private static bool TryLoadSkinnedMeshAsset(string fullPath, out SkinnedMeshAsset skinnedMeshAsset)
    {
        skinnedMeshAsset = new SkinnedMeshAsset();
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["skeleton_asset_id"] == null && document["geometry_asset_id"] == null && document["rigged_model_asset_id"] == null)
            {
                return false;
            }

            skinnedMeshAsset.Load(document);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}