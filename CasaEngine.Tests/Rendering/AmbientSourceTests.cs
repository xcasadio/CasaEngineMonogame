using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class AmbientSourceTests
{
    [Fact]
    public void WorldEnvironmentSettings_DefaultAmbientSourceIsAuto()
    {
        Assert.Equal(AmbientSource.Auto, new WorldEnvironmentSettings().AmbientSource);
    }

    [Fact]
    public void ResetToDefaults_RestoresAuto()
    {
        var settings = new WorldEnvironmentSettings { AmbientSource = AmbientSource.Flat };

        settings.ResetToDefaults();

        Assert.Equal(AmbientSource.Auto, settings.AmbientSource);
    }

    [Fact]
    public void Clone_CopiesAmbientSource()
    {
        var settings = new WorldEnvironmentSettings { AmbientSource = AmbientSource.Flat };

        Assert.Equal(AmbientSource.Flat, settings.Clone().AmbientSource);
    }

    [Fact]
    public void CopyFrom_CopiesAmbientSourceBothWays()
    {
        var flat = new WorldEnvironmentSettings { AmbientSource = AmbientSource.Flat };
        var target = new WorldEnvironmentSettings();

        target.CopyFrom(flat);
        Assert.Equal(AmbientSource.Flat, target.AmbientSource);

        target.CopyFrom(new WorldEnvironmentSettings());
        Assert.Equal(AmbientSource.Auto, target.AmbientSource);
    }

    [Fact]
    public void Save_OmitsAmbientSourceWhenAuto_SoExistingWorldsKeepTheirFormat()
    {
        var saved = new JObject();

        new WorldEnvironmentSettings().Save(saved);

        Assert.False(saved.ContainsKey("ambient_source"));
    }

    [Fact]
    public void Save_WritesAmbientSourceWhenFlat_AndLoadReadsItBack()
    {
        var saved = new JObject();
        new WorldEnvironmentSettings { AmbientSource = AmbientSource.Flat }.Save(saved);

        Assert.Equal("Flat", (string?)saved["ambient_source"]);

        var loaded = new WorldEnvironmentSettings();
        loaded.Load(saved);
        Assert.Equal(AmbientSource.Flat, loaded.AmbientSource);
    }

    [Fact]
    public void Save_RemovesAStaleAmbientSourceWhenBackToAuto()
    {
        var saved = new JObject { ["ambient_source"] = "Flat" };

        new WorldEnvironmentSettings().Save(saved);

        Assert.False(saved.ContainsKey("ambient_source"));
    }

    [Fact]
    public void Load_TreatsAnAbsentFieldAsAuto_EvenOnSettingsThatWereFlat()
    {
        var settings = new WorldEnvironmentSettings { AmbientSource = AmbientSource.Flat };

        settings.Load(new JObject());

        Assert.Equal(AmbientSource.Auto, settings.AmbientSource);
    }

    [Fact]
    public void Load_ReadsTheAmbientSourceCaseInsensitively()
    {
        var settings = new WorldEnvironmentSettings();

        settings.Load(new JObject { ["ambient_source"] = "flat" });

        Assert.Equal(AmbientSource.Flat, settings.AmbientSource);
    }

    [Theory]
    [InlineData(AmbientSource.Auto, false)]
    [InlineData(AmbientSource.Flat, true)]
    public void EnvironmentResolver_CarriesTheAmbientSourceOfTheWorldToTheResolvedSettings(AmbientSource source, bool expectedFlat)
    {
        var world = new World();
        world.EnvironmentSettings.AmbientSource = source;
        var view = new RenderView(world, new PerspectiveCameraComponent(), new StubRenderSurface());

        var resolved = EnvironmentResolver.Resolve(view);

        Assert.Equal(source, resolved.AmbientSource);
        Assert.Equal(expectedFlat, resolved.UsesFlatAmbient);
    }

    [Fact]
    public void EnvironmentResolver_UsesTheViewOverrideAmbientSource()
    {
        var world = new World();
        var view = new RenderView(world, new PerspectiveCameraComponent(), new StubRenderSurface())
        {
            EnvironmentOverride = new WorldEnvironmentSettings { AmbientSource = AmbientSource.Flat },
        };

        Assert.True(EnvironmentResolver.Resolve(view).UsesFlatAmbient);
    }

    [Fact]
    public void ResolvedEnvironmentSettings_AutoIsTheDefaultAndTheLegacyValue()
    {
        Assert.False(default(ResolvedEnvironmentSettings).UsesFlatAmbient);
        Assert.False(ResolvedEnvironmentSettings.CreateLegacy(Color.Black, Vector3.Zero).UsesFlatAmbient);
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public bool IsBackBuffer => false;

        public Rectangle ViewportRect => new(0, 0, 64, 64);

        public RenderTarget2D? RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }
}
