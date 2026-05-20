using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Particle System")]
public class ParticleSystemComponent : SceneComponent
{
    private ParticleEffectAsset? _particleEffectAsset;
    private ParticleRuntimeInstance? _runtimeInstance;
    private float _simulationSpeed = 1.0f;
    private float _emissionScale = 1.0f;

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
        }
    }

    public ParticleEffectAsset? ParticleEffectAsset => _particleEffectAsset;

    public ParticleRuntimeInstance? RuntimeInstance => _runtimeInstance;

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
        LoadParticleEffectAsset();
    }

    public override ParticleSystemComponent Clone()
        => new(this);

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
        _runtimeInstance = new ParticleRuntimeInstance(particleEffectAsset)
        {
            SimulationSpeed = SimulationSpeed,
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

    private void LoadParticleEffectAsset()
    {
        if (ParticleEffectAssetId == Guid.Empty || Owner?.World?.Game?.AssetContentManager == null)
        {
            return;
        }

        ParticleEffectAsset particleEffectAsset = Owner.World.Game.AssetContentManager.Load<ParticleEffectAsset>(ParticleEffectAssetId);
        RebuildRuntime(particleEffectAsset);
    }
}