using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleSystemComponentTests
{
    [Fact]
    public void Clone_CopiesAuthoringPropertiesWithoutRuntime()
    {
        var assetId = Guid.NewGuid();
        var component = new ParticleSystemComponent
        {
            ParticleEffectAssetId = assetId,
            PlayOnStart = false,
            Looping = false,
            SimulateInEditor = false,
            SimulationSpeed = 2.0f,
            ColorTint = Color.CornflowerBlue,
            EmissionScale = 0.5f,
        };

        ParticleSystemComponent clone = component.Clone();

        Assert.NotSame(component, clone);
        Assert.Equal(assetId, clone.ParticleEffectAssetId);
        Assert.False(clone.PlayOnStart);
        Assert.False(clone.Looping);
        Assert.False(clone.SimulateInEditor);
        Assert.Equal(2.0f, clone.SimulationSpeed);
        Assert.Equal(Color.CornflowerBlue, clone.ColorTint);
        Assert.Equal(0.5f, clone.EmissionScale);
        Assert.Null(clone.RuntimeInstance);
    }

    [Fact]
    public void Load_ReadsParticleSystemProperties()
    {
        var assetId = Guid.NewGuid();
        var component = new ParticleSystemComponent();

        component.Load(CreateComponentNode(assetId));

        Assert.Equal(assetId, component.ParticleEffectAssetId);
        Assert.False(component.PlayOnStart);
        Assert.False(component.Looping);
        Assert.True(component.SimulateInEditor);
        Assert.Equal(1.5f, component.SimulationSpeed);
        Assert.Equal(0.25f, component.EmissionScale);
        Assert.Equal(new Color(10, 20, 30, 40), component.ColorTint);
    }

    [Fact]
    public void SimulationSpeed_RejectsInvalidValues()
    {
        var component = new ParticleSystemComponent();

        Assert.Throws<ArgumentOutOfRangeException>(() => component.SimulationSpeed = -0.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => component.SimulationSpeed = float.NaN);
    }

    [Fact]
    public void EmissionScale_RejectsInvalidValues()
    {
        var component = new ParticleSystemComponent();

        Assert.Throws<ArgumentOutOfRangeException>(() => component.EmissionScale = -0.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => component.EmissionScale = float.PositiveInfinity);
    }

    [Fact]
    public void Update_AdvancesRuntimeOncePerWorldSequence()
    {
        ParticleSystemComponent component = CreateAttachedComponent(out _);
        component.SetParticleEffectAsset(CreateRuntimeAsset(rateOverTime: 1.0f));

        component.Update(1.0f);
        int emittedAfterFirstUpdate = component.LastEmittedCount;
        float ageAfterFirstUpdate = component.RuntimeInstance!.Emitters[0].GetParticle(0).Age;
        component.Update(1.0f);

        Assert.Equal(1, emittedAfterFirstUpdate);
        Assert.Equal(1, component.RuntimeInstance.AliveCount);
        Assert.Equal(ageAfterFirstUpdate, component.RuntimeInstance.Emitters[0].GetParticle(0).Age);
    }

    [Fact]
    public void Update_SkipsRuntimeWhenOwnerIsDisabled()
    {
        ParticleSystemComponent component = CreateAttachedComponent(out Entity entity);
        component.SetParticleEffectAsset(CreateRuntimeAsset(rateOverTime: 1.0f));
        entity.IsEnabled = false;

        component.Update(1.0f);

        Assert.Equal(0, component.LastEmittedCount);
        Assert.Equal(0, component.RuntimeInstance!.AliveCount);
    }

    [Fact]
    public void Update_DoesNotEmitWhenPlayOnStartIsFalse()
    {
        ParticleSystemComponent component = CreateAttachedComponent(out _);
        component.PlayOnStart = false;
        component.SetParticleEffectAsset(CreateRuntimeAsset(rateOverTime: 1.0f));

        component.Update(1.0f);

        Assert.Equal(0, component.LastEmittedCount);
        Assert.Equal(0, component.RuntimeInstance!.AliveCount);
    }

    private static JObject CreateComponentNode(Guid assetId)
        => new()
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Particle Component",
            ["type"] = nameof(ParticleSystemComponent),
            ["coordinates"] = new JObject
            {
                ["position"] = CreateVector3Node(Vector3.Zero),
                ["scale"] = CreateVector3Node(Vector3.One),
                ["rotation"] = new JObject
                {
                    ["x"] = 0.0f,
                    ["y"] = 0.0f,
                    ["z"] = 0.0f,
                    ["w"] = 1.0f,
                },
            },
            ["children_component"] = new JArray(),
            ["particle_effect_asset_id"] = assetId.ToString(),
            ["play_on_start"] = false,
            ["looping"] = false,
            ["simulate_in_editor"] = true,
            ["simulation_speed"] = 1.5f,
            ["emission_scale"] = 0.25f,
            ["color_tint"] = new JObject
            {
                ["r"] = 10,
                ["g"] = 20,
                ["b"] = 30,
                ["a"] = 40,
            },
        };

    private static JObject CreateVector3Node(Vector3 value)
        => new()
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };

    private static ParticleSystemComponent CreateAttachedComponent(out Entity entity)
    {
        var world = new World();
        entity = new Entity();
        var component = new ParticleSystemComponent();
        entity.RootComponent = component;
        entity.InitializeWithWorld(world);
        return component;
    }

    private static ParticleEffectAsset CreateRuntimeAsset(float rateOverTime)
    {
        var asset = new ParticleEffectAsset();
        var emitter = new ParticleEmitterDefinition
        {
            Duration = 10.0f,
            MaxParticles = 8,
        };

        emitter.Emission.RateOverTime = rateOverTime;
        emitter.Initial.Lifetime = FloatRange.Constant(10.0f);
        emitter.Initial.Speed = FloatRange.Constant(0.0f);
        emitter.Initial.Size = Vector2Range.Constant(Vector2.One);
        emitter.Initial.Rotation = FloatRange.Constant(0.0f);
        emitter.Initial.AngularVelocity = FloatRange.Constant(0.0f);
        emitter.Initial.StartColor = ColorGradient.Constant(Color.White);
        emitter.Simulation.Gravity = Vector3.Zero;
        emitter.Simulation.GravityScale = 0.0f;
        emitter.Simulation.Drag = 0.0f;
        emitter.Simulation.SizeOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.AlphaOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.VelocityOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.ColorOverLifetime = ColorGradient.Constant(Color.White);
        asset.Emitters.Add(emitter);
        return asset;
    }
}