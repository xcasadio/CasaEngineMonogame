using CasaEngine.Framework.Scene.Entities.Components;
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
}