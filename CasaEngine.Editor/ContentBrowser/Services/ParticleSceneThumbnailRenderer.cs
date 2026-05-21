using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.ContentBrowser.Services;

internal readonly record struct ParticleThumbnailRenderResult(
    long RequestId,
    string Path,
    byte[]? ImageBytes,
    Point? SourceSize,
    bool Succeeded);

internal interface IParticleThumbnailRenderer : IDisposable
{
    void Enqueue(string path, long requestId);

    void Update();

    bool TryDequeueCompleted(out ParticleThumbnailRenderResult result);
}

internal sealed class ParticleSceneThumbnailRenderer : IParticleThumbnailRenderer
{
    private sealed class PendingThumbnailRequest
    {
        public PendingThumbnailRequest(long requestId, string path)
        {
            RequestId = requestId;
            Path = path;
        }

        public long RequestId { get; }

        public string Path { get; }
    }

    private const float WarmupSeconds = 0.45f;
    private const int WarmupStepCount = 15;

    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly int _thumbnailSize;
    private readonly WorldEnvironmentSettings _environmentOverride = PreviewEnvironmentFactory.CreateNeutralPreview(EditorThemePalette.PreviewClearColor);
    private readonly PreviewWorldDriver _previewWorldDriver;
    private readonly Queue<PendingThumbnailRequest> _pendingRequests = new();
    private readonly Queue<ParticleThumbnailRenderResult> _completedResults = new();

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private Entity? _previewEntity;
    private ParticleSystemComponent? _particleComponent;
    private Entity? _cameraEntity;
    private CameraLookAtComponent? _camera;
    private PendingThumbnailRequest? _activeRequest;
    private bool _disposed;

    public ParticleSceneThumbnailRenderer(GraphicsDevice graphicsDevice, int thumbnailSize, HostedEditorGameAdapter editorRuntime)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(editorRuntime);

        _graphicsDevice = graphicsDevice;
        _thumbnailSize = Math.Max(32, thumbnailSize);
        _editorRuntime = editorRuntime;
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "ParticleThumbnailPreviewWorld",
            UpdateMode = PreviewWorldUpdateMode.Manual,
        });
    }

    public void Enqueue(string path, long requestId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _pendingRequests.Enqueue(new PendingThumbnailRequest(requestId, path));
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        if (_activeRequest != null)
        {
            if (!TryCompleteActiveRequest())
            {
                return;
            }

            _activeRequest = null;
        }

        if (_pendingRequests.Count > 0)
        {
            StartNextRequest(_pendingRequests.Dequeue());
        }
    }

    public bool TryDequeueCompleted(out ParticleThumbnailRenderResult result)
    {
        if (_completedResults.Count > 0)
        {
            result = _completedResults.Dequeue();
            return true;
        }

        result = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingRequests.Clear();
        _completedResults.Clear();
        _activeRequest = null;

        if (_renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _surface?.Dispose();
        _surface = null;
        _previewWorldDriver.Dispose();
        _particleComponent = null;
        _previewEntity = null;
        _camera = null;
        _cameraEntity = null;
    }

    private void StartNextRequest(PendingThumbnailRequest request)
    {
        EnsurePreviewSceneCreated();
        EnsureRenderViewCreated();

        ParticleEffectAsset? particleAsset = TryLoadParticleAsset(request.Path);
        if (particleAsset == null || _particleComponent == null || _previewWorldDriver.World == null)
        {
            _completedResults.Enqueue(new ParticleThumbnailRenderResult(request.RequestId, request.Path, null, null, false));
            return;
        }

        _particleComponent.Looping = HasLoopingEmitter(particleAsset);
        _particleComponent.SimulationSpeed = 1.0f;
        _particleComponent.ColorTint = Color.White;
        _particleComponent.SetParticleEffectAsset(particleAsset);
        _particleComponent.Restart(clearParticles: true);
        ResetPreviewTransform(particleAsset);
        WarmUpSimulation(_previewWorldDriver.World);

        if (_renderView == null || _surface?.RenderTarget == null)
        {
            _completedResults.Enqueue(new ParticleThumbnailRenderResult(request.RequestId, request.Path, null, null, false));
            return;
        }

        _renderView.RenderStats.RenderedThisFrame = false;
        _renderView.Invalidate();
        _activeRequest = request;
    }

    private bool TryCompleteActiveRequest()
    {
        if (_activeRequest == null)
        {
            return true;
        }

        if (_renderView == null || _surface?.RenderTarget == null)
        {
            _completedResults.Enqueue(new ParticleThumbnailRenderResult(_activeRequest.RequestId, _activeRequest.Path, null, null, false));
            return true;
        }

        if (!_renderView.RenderStats.RenderedThisFrame)
        {
            return false;
        }

        try
        {
            using var output = new MemoryStream();
            RenderTarget2D renderTarget = _surface.RenderTarget;
            renderTarget.SaveAsPng(output, renderTarget.Width, renderTarget.Height);
            _completedResults.Enqueue(new ParticleThumbnailRenderResult(
                _activeRequest.RequestId,
                _activeRequest.Path,
                output.ToArray(),
                new Point(renderTarget.Width, renderTarget.Height),
                true));
        }
        catch
        {
            _completedResults.Enqueue(new ParticleThumbnailRenderResult(_activeRequest.RequestId, _activeRequest.Path, null, null, false));
        }
        finally
        {
            _renderView.RenderStats.RenderedThisFrame = false;
        }

        return true;
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
                Name = "ParticleThumbnailPreviewEntity",
            };

            _particleComponent = new ParticleSystemComponent
            {
                PlayOnStart = false,
                Looping = true,
                SimulateInEditor = true,
                SimulationSpeed = 1.0f,
                ColorTint = Color.White,
            };
            _previewEntity.RootComponent = _particleComponent;
            world.AddEntity(_previewEntity);
        });
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
            Name = "ParticleThumbnailCamera",
            IsVisible = false,
        };

        _camera = new CameraLookAtComponent();
        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(_previewWorldDriver.World!);
        _camera.OnScreenResized(_thumbnailSize, _thumbnailSize);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _thumbnailSize,
            _thumbnailSize,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        ViewId viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "Particle Thumbnail",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = EditorThemePalette.PreviewClearColor,
            EnvironmentOverride = _environmentOverride,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out RenderView? renderView))
        {
            throw new InvalidOperationException("The particle thumbnail renderer could not create its offscreen render view.");
        }

        _renderView = renderView;
    }

    private static ParticleEffectAsset? TryLoadParticleAsset(string path)
    {
        try
        {
            var node = JObject.Parse(File.ReadAllText(path));
            var asset = new ParticleEffectAsset();
            asset.Load(node);
            asset.FileName = path;
            asset.AssetId = asset.Id;

            IReadOnlyList<string> errors = asset.Validate();
            return errors.Count == 0 ? asset : null;
        }
        catch
        {
            return null;
        }
    }

    private void WarmUpSimulation(World world)
    {
        float stepSeconds = WarmupSeconds / WarmupStepCount;
        for (int stepIndex = 0; stepIndex < WarmupStepCount; stepIndex++)
        {
            world.Update(stepSeconds);
        }
    }

    private void ResetPreviewTransform(ParticleEffectAsset particleAsset)
    {
        if (_particleComponent != null)
        {
            _particleComponent.LocalPosition = Vector3.Zero;
            _particleComponent.LocalOrientation = Quaternion.Identity;
            _particleComponent.LocalScale = Vector3.One;
        }

        ConfigureCamera(particleAsset);
    }

    private void ConfigureCamera(ParticleEffectAsset particleAsset)
    {
        if (_camera == null)
        {
            return;
        }

        float radius = CalculatePreviewRadius(particleAsset);
        Vector3 target = new(0.0f, radius * 0.25f, 0.0f);
        Vector3 position = new(0.0f, radius * 0.45f, Math.Max(3.0f, radius * 3.0f));
        _camera.SetPositionAndTarget(position, target);
    }

    private static float CalculatePreviewRadius(ParticleEffectAsset particleAsset)
    {
        if (particleAsset.Emitters.Count == 0)
        {
            return 1.5f;
        }

        float radius = 1.5f;
        for (int emitterIndex = 0; emitterIndex < particleAsset.Emitters.Count; emitterIndex++)
        {
            ParticleEmitterDefinition emitter = particleAsset.Emitters[emitterIndex];
            radius = MathF.Max(radius, emitter.Shape.Radius + MathF.Max(emitter.Shape.Size.X, emitter.Shape.Size.Y) * 0.5f);
            radius = MathF.Max(radius, MathF.Max(emitter.Initial.Size.Max.X, emitter.Initial.Size.Max.Y));
            radius = MathF.Max(radius, emitter.Initial.Speed.Max * emitter.Initial.Lifetime.Max * 0.35f);
        }

        return MathHelper.Clamp(radius, 1.5f, 12.0f);
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
}