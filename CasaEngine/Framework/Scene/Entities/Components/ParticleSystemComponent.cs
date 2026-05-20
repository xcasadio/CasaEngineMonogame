using System.ComponentModel;
using System.Diagnostics;
using CasaEngine.Core.Math.Geometry;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Rendering;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Particle System")]
public class ParticleSystemComponent : SceneComponent
{
    private ParticleEffectAsset? _particleEffectAsset;
    private ParticleRuntimeInstance? _runtimeInstance;
    private ParticleRendererComponent? _particleRendererComponent;
    private readonly List<ParticleRenderPacket> _renderPackets = new(64);
    private float _simulationSpeed = 1.0f;
    private float _emissionScale = 1.0f;
    private int _lastUpdateSequence = -1;

    public Guid ParticleEffectAssetId { get; set; } = Guid.Empty;

    public bool PlayOnStart { get; set; } = true;

    public bool Looping { get; set; } = true;

    public bool SimulateInEditor { get; set; } = true;

    public float SimulationSpeed
    {
        get => _simulationSpeed;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Simulation speed must be finite and non-negative.");
            }

            _simulationSpeed = value;
            if (_runtimeInstance != null)
            {
                _runtimeInstance.SimulationSpeed = value;
            }
        }
    }

    public Color ColorTint { get; set; } = Color.White;

    public float EmissionScale
    {
        get => _emissionScale;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Emission scale must be finite and non-negative.");
            }

            _emissionScale = value;
            if (_runtimeInstance != null)
            {
                _runtimeInstance.EmissionScale = value;
            }
        }
    }

    public ParticleEffectAsset? ParticleEffectAsset => _particleEffectAsset;

    public ParticleRuntimeInstance? RuntimeInstance => _runtimeInstance;

    public int LastEmittedCount { get; private set; }

    public int LastExtractedPacketCount { get; private set; }

    public double LastExtractionCpuMilliseconds { get; private set; }

    public bool AlwaysVisible
    {
        get
        {
            if (_runtimeInstance != null)
            {
                for (int emitterIndex = 0; emitterIndex < _runtimeInstance.Emitters.Length; emitterIndex++)
                {
                    if (_runtimeInstance.Emitters[emitterIndex].AlwaysVisible)
                    {
                        return true;
                    }
                }
            }

            if (_particleEffectAsset == null)
            {
                return false;
            }

            for (int emitterIndex = 0; emitterIndex < _particleEffectAsset.Emitters.Count; emitterIndex++)
            {
                if (_particleEffectAsset.Emitters[emitterIndex].Renderer.AlwaysVisible)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public ParticleSystemComponent()
    {
    }

    public ParticleSystemComponent(ParticleSystemComponent other) : base(other)
    {
        ParticleEffectAssetId = other.ParticleEffectAssetId;
        PlayOnStart = other.PlayOnStart;
        Looping = other.Looping;
        SimulateInEditor = other.SimulateInEditor;
        SimulationSpeed = other.SimulationSpeed;
        ColorTint = other.ColorTint;
        EmissionScale = other.EmissionScale;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        if (world.Game != null)
        {
            _particleRendererComponent = world.Game.GetGameComponent<ParticleRendererComponent>();
        }

        LoadParticleEffectAsset();
    }

    public override ParticleSystemComponent Clone()
        => new(this);

    public override BoundingBox GetBoundingBox()
    {
        if (_runtimeInstance != null)
        {
            _runtimeInstance.WorldMatrix = WorldMatrixWithScale;
            _runtimeInstance.UpdateBounds();
            if (_runtimeInstance.HasBounds)
            {
                return _runtimeInstance.Bounds;
            }
        }

        if (_particleEffectAsset == null)
        {
            return base.GetBoundingBox();
        }

        BoundingBox authoringBounds = CalculateAuthoringFallbackBounds(_particleEffectAsset);
        return authoringBounds.Valid() ? authoringBounds.Transform(WorldMatrixWithScale) : base.GetBoundingBox();
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (!CanUpdateRuntime())
        {
            LastEmittedCount = 0;
            return;
        }

        _runtimeInstance!.WorldMatrix = WorldMatrixWithScale;
        LastEmittedCount = _runtimeInstance.Update(elapsedTime);
        _lastUpdateSequence = Owner!.World.UpdateSequence;
        IsBoundingBoxDirty = true;
    }

    public override void Draw(float elapsedTime)
    {
        base.Draw(elapsedTime);

        if (_runtimeInstance == null || Owner?.World?.CurrentRenderFrame is not { } frame)
        {
            LastExtractedPacketCount = 0;
            LastExtractionCpuMilliseconds = 0.0;
            return;
        }

        if (_particleRendererComponent == null && Owner.World.Game != null)
        {
            _particleRendererComponent = Owner.World.Game.GetGameComponent<ParticleRendererComponent>();
        }

        if (_particleRendererComponent == null)
        {
            LastExtractedPacketCount = 0;
            LastExtractionCpuMilliseconds = 0.0;
            return;
        }

        _runtimeInstance.WorldMatrix = WorldMatrixWithScale;
        long extractionStartTimestamp = Stopwatch.GetTimestamp();
        LastExtractedPacketCount = ParticleRenderPacketExtractor.Extract(_runtimeInstance, frame.CameraPosition, ColorTint, _renderPackets);
        LastExtractionCpuMilliseconds = GetElapsedMilliseconds(extractionStartTimestamp);
        _particleRendererComponent.Submit(_renderPackets);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        ParticleEffectAssetId = element["particle_effect_asset_id"]?.GetGuid() ?? ParticleEffectAssetId;
        PlayOnStart = element["play_on_start"]?.GetBoolean() ?? PlayOnStart;
        Looping = element["looping"]?.GetBoolean() ?? Looping;
        SimulateInEditor = element["simulate_in_editor"]?.GetBoolean() ?? SimulateInEditor;
        SimulationSpeed = element["simulation_speed"]?.GetSingle() ?? SimulationSpeed;
        EmissionScale = element["emission_scale"]?.GetSingle() ?? EmissionScale;
        ColorTint = element["color_tint"] is { } colorTintNode ? colorTintNode.GetColor() : ColorTint;
    }

    protected void RebuildRuntime(ParticleEffectAsset particleEffectAsset)
    {
        _particleEffectAsset = particleEffectAsset;
        _renderPackets.Clear();
        LastEmittedCount = 0;
        LastExtractedPacketCount = 0;
        LastExtractionCpuMilliseconds = 0.0;
        _lastUpdateSequence = -1;
        _runtimeInstance = new ParticleRuntimeInstance(particleEffectAsset)
        {
            SimulationSpeed = SimulationSpeed,
            EmissionScale = EmissionScale,
            WorldMatrix = WorldMatrixWithScale,
        };

        for (int emitterIndex = 0; emitterIndex < _runtimeInstance.Emitters.Length; emitterIndex++)
        {
            _runtimeInstance.Emitters[emitterIndex].Looping = Looping;
        }

        if (PlayOnStart)
        {
            _runtimeInstance.Play();
        }

        IsBoundingBoxDirty = true;
    }

    public void SetParticleEffectAsset(ParticleEffectAsset particleEffectAsset)
    {
        ArgumentNullException.ThrowIfNull(particleEffectAsset);
        RebuildRuntime(particleEffectAsset);
    }

    public void ClearParticleEffectAsset()
    {
        ParticleEffectAssetId = Guid.Empty;
        _particleEffectAsset = null;
        _runtimeInstance = null;
        _renderPackets.Clear();
        LastEmittedCount = 0;
        LastExtractedPacketCount = 0;
        LastExtractionCpuMilliseconds = 0.0;
        IsBoundingBoxDirty = true;
    }

    public void Play()
        => _runtimeInstance?.Play();

    public void Pause()
        => _runtimeInstance?.Pause();

    public void Stop(bool clearParticles = true)
    {
        _runtimeInstance?.Stop(clearParticles);
        IsBoundingBoxDirty = true;
    }

    public void Restart(bool clearParticles = true)
    {
        _runtimeInstance?.Restart(clearParticles);
        IsBoundingBoxDirty = true;
    }

    public int Emit(int particleCount)
    {
        if (_runtimeInstance == null || particleCount <= 0)
        {
            return 0;
        }

        _runtimeInstance.WorldMatrix = WorldMatrixWithScale;
        int emittedCount = _runtimeInstance.Emit(particleCount);
        IsBoundingBoxDirty = emittedCount > 0 || IsBoundingBoxDirty;
        return emittedCount;
    }

    private void LoadParticleEffectAsset()
    {
        if (ParticleEffectAssetId == Guid.Empty || Owner?.World?.Game?.AssetContentManager == null)
        {
            return;
        }

        ParticleEffectAsset particleEffectAsset = Owner.World.Game.AssetContentManager.Load<ParticleEffectAsset>(ParticleEffectAssetId);
        RebuildRuntime(particleEffectAsset);
    }

    private bool CanUpdateRuntime()
    {
        if (_runtimeInstance == null || Owner?.World == null || !Owner.IsEnabled)
        {
            return false;
        }

        if (Owner.World.UpdateSequence == _lastUpdateSequence)
        {
            return false;
        }

        if (Owner.World.Game?.ExecutionPolicy.IsEditorPreview == true && !SimulateInEditor)
        {
            return false;
        }

        return true;
    }

    private static BoundingBox CalculateAuthoringFallbackBounds(ParticleEffectAsset particleEffectAsset)
    {
        BoundingBox bounds = BoundingBoxHelper.Create();
        for (int emitterIndex = 0; emitterIndex < particleEffectAsset.Emitters.Count; emitterIndex++)
        {
            ParticleEmitterDefinition emitter = particleEffectAsset.Emitters[emitterIndex];
            float shapeRadius = CalculateShapeRadius(emitter.Shape);
            float particleRadius = MathF.Max(emitter.Initial.Size.Max.X, emitter.Initial.Size.Max.Y) * 0.5f;
            float radius = MathF.Max(0.5f, shapeRadius + particleRadius);
            bounds.ExpandBy(new Vector3(-radius, -radius, -radius));
            bounds.ExpandBy(new Vector3(radius, radius, radius));
        }

        return bounds;
    }

    private static float CalculateShapeRadius(ParticleShapeModule shape)
    {
        return shape.ShapeType switch
        {
            ParticleShapeType.Circle or ParticleShapeType.Sphere or ParticleShapeType.Cone => MathF.Max(0.0f, shape.Radius),
            ParticleShapeType.Box => MathF.Max(shape.Size.X, MathF.Max(shape.Size.Y, shape.Size.Z)) * 0.5f,
            _ => 0.0f,
        };
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
        => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
}