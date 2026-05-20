using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleEffectAssetJsonSerializerTests
{
    private static readonly Guid SampleProjectSmokePuffAssetId = Guid.Parse("4cbd68f9-ad8e-4f5f-9ad7-8a0c85a1da61");

    [Fact]
    public void SaveLoad_RoundTripsEmitterModules()
    {
        var textureAssetId = Guid.NewGuid();
        var asset = CreateAsset(textureAssetId);
        var node = new JObject();

        ParticleEffectAssetJsonSerializer.Save(asset, node);

        var loaded = new ParticleEffectAsset();
        loaded.Load(node);

        Assert.Equal(asset.Id, loaded.Id);
        Assert.Equal("SparkBurst", loaded.Name);
        Assert.Equal(ParticleEffectAsset.CurrentVersion, loaded.Version);
        Assert.Single(loaded.Emitters);

        ParticleEmitterDefinition emitter = loaded.Emitters[0];
        Assert.Equal("Sparks", emitter.Name);
        Assert.False(emitter.Looping);
        Assert.Equal(2.5f, emitter.Duration);
        Assert.Equal(128, emitter.MaxParticles);
        Assert.Equal(12.0f, emitter.Emission.RateOverTime);
        Assert.Single(emitter.Emission.Bursts);
        Assert.Equal(20, emitter.Emission.Bursts[0].CountMin);
        Assert.Equal(30, emitter.Emission.Bursts[0].CountMax);
        Assert.Equal(ParticleShapeType.Cone, emitter.Shape.ShapeType);
        Assert.Equal(20.0f, emitter.Shape.AngleDegrees);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), emitter.Shape.Size);
        Assert.Equal(new FloatRange(0.25f, 0.75f), emitter.Initial.Lifetime);
        Assert.Equal(new Vector2(0.5f, 0.5f), emitter.Initial.Size.Min);
        Assert.Equal(ParticleSimulationSpace.World, emitter.Simulation.SimulationSpace);
        Assert.Equal(0.5f, emitter.Simulation.Drag);
        Assert.Equal(0.25f, emitter.Simulation.AlphaOverLifetime.Evaluate(0.75f));
        Assert.Equal(textureAssetId, emitter.Renderer.TextureAssetId);
        Assert.Equal(ParticleBlendMode.Additive, emitter.Renderer.BlendMode);
        Assert.Equal(ParticleSortMode.Distance, emitter.Renderer.SortMode);
        Assert.True(emitter.Renderer.AlwaysVisible);
    }

    [Fact]
    public void Loader_SupportsParticleExtensionAndLoadsAsset()
    {
        string fileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + Constants.FileNameExtensions.Particle);
        try
        {
            var asset = CreateAsset(Guid.Empty);
            var node = new JObject();
            ParticleEffectAssetJsonSerializer.Save(asset, node);
            File.WriteAllText(fileName, node.ToString(Formatting.Indented));

            var loader = new ParticleEffectAssetLoader();
            object? loaded = loader.LoadAsset(fileName, new AssetContentManager());

            Assert.True(loader.IsFileSupported(fileName));
            var loadedAsset = Assert.IsType<ParticleEffectAsset>(loaded);
            Assert.Equal("SparkBurst", loadedAsset.Name);
            Assert.Single(loadedAsset.Emitters);
        }
        finally
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
    }

    [Fact]
    public void SampleProjectParticleAsset_LoadsThroughAssetContentManager()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sampleProjectPath = Path.Combine(repositoryRoot, "Projects", "SampleProject");
        string? oldProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            AssetCatalog.Load(Path.Combine(sampleProjectPath, "AssetInfos.json"));
            EngineEnvironment.ProjectPath = sampleProjectPath;

            var assetContentManager = new AssetContentManager();
            assetContentManager.RegisterAssetLoader(typeof(ParticleEffectAsset), new ParticleEffectAssetLoader());

            ParticleEffectAsset asset = assetContentManager.Load<ParticleEffectAsset>(SampleProjectSmokePuffAssetId, cache: false);

            Assert.Equal("SmokePuff_Minimal", asset.Name);
            Assert.Equal("Particles\\SmokePuff_Minimal.particle", asset.FileName);
            Assert.Single(asset.Emitters);
            Assert.Equal("Smoke Puff", asset.Emitters[0].Name);
            Assert.Equal(64, asset.Emitters[0].MaxParticles);
        }
        finally
        {
            EngineEnvironment.ProjectPath = oldProjectPath;
        }
    }

    [Fact]
    public void CanMigrate_RejectsFutureVersions()
    {
        Assert.True(ParticleEffectAssetJsonSerializer.CanMigrate(ParticleEffectAsset.CurrentVersion));
        Assert.False(ParticleEffectAssetJsonSerializer.CanMigrate(ParticleEffectAsset.CurrentVersion + 1));
    }

    private static ParticleEffectAsset CreateAsset(Guid textureAssetId)
    {
        var asset = new ParticleEffectAsset
        {
            Name = "SparkBurst",
        };

        var emitter = new ParticleEmitterDefinition
        {
            Name = "Sparks",
            Duration = 2.5f,
            Looping = false,
            StartDelay = 0.1f,
            MaxParticles = 128,
        };

        emitter.Emission.RateOverTime = 12.0f;
        emitter.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.2f,
            CountMin = 20,
            CountMax = 30,
        });
        emitter.Shape.ShapeType = ParticleShapeType.Cone;
        emitter.Shape.Size = new Vector3(1.0f, 2.0f, 3.0f);
        emitter.Shape.Radius = 0.75f;
        emitter.Shape.AngleDegrees = 20.0f;
        emitter.Initial.Lifetime = new FloatRange(0.25f, 0.75f);
        emitter.Initial.Size = new Vector2Range(new Vector2(0.5f, 0.5f), new Vector2(2.0f, 2.0f));
        emitter.Simulation.SimulationSpace = ParticleSimulationSpace.World;
        emitter.Simulation.Drag = 0.5f;
        emitter.Simulation.AlphaOverLifetime = FloatCurve.FadeOut();
        emitter.Renderer.TextureAssetId = textureAssetId;
        emitter.Renderer.BlendMode = ParticleBlendMode.Additive;
        emitter.Renderer.SortMode = ParticleSortMode.Distance;
        emitter.Renderer.AlwaysVisible = true;

        asset.Emitters.Add(emitter);
        return asset;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate CasaEngineMonogame repository root.");
    }
}