using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ColorPipelineSettingsTests
{
    [Fact]
    public void Defaults_KeepTheLegacyPipeline()
    {
        var settings = new WorldEnvironmentSettings().ColorPipeline;

        Assert.Equal(ColorPipelineMode.Legacy, settings.Mode);
        Assert.Equal(TonemapOperator.AcesApprox, settings.Tonemapper);
        Assert.Equal(1.0f, settings.Exposure);
        Assert.True(settings.IsDefault);
    }

    [Fact]
    public void Exposure_NaNInfinityAndNegativeBecomeZero()
    {
        var settings = new ColorPipelineSettings { Exposure = float.NaN };
        Assert.Equal(0.0f, settings.Exposure);

        settings.Exposure = -2.0f;
        Assert.Equal(0.0f, settings.Exposure);

        settings.Exposure = float.PositiveInfinity;
        Assert.Equal(0.0f, settings.Exposure);

        settings.Exposure = 2.5f;
        Assert.Equal(2.5f, settings.Exposure);
    }

    [Fact]
    public void Clone_CopiesTheColorPipeline()
    {
        var settings = CreateNonDefault();

        var clone = settings.Clone();

        AssertNonDefault(clone);
    }

    [Fact]
    public void CopyFrom_CopiesTheColorPipelineBothWays()
    {
        var target = new WorldEnvironmentSettings();

        target.CopyFrom(CreateNonDefault());
        AssertNonDefault(target);

        target.CopyFrom(new WorldEnvironmentSettings());
        Assert.True(target.ColorPipeline.IsDefault);
    }

    [Fact]
    public void Save_OmitsTheKeyWhenDefault_SoExistingWorldsKeepTheirFormat()
    {
        var saved = new JObject();

        new WorldEnvironmentSettings().Save(saved);

        Assert.False(saved.ContainsKey("color_pipeline"));
    }

    [Fact]
    public void Save_WritesTheKeyWhenAFieldDiffers_AndLoadReadsItBack()
    {
        var saved = new JObject();
        CreateNonDefault().Save(saved);

        var node = Assert.IsType<JObject>(saved["color_pipeline"]);
        Assert.Equal("LinearHdr", (string?)node["mode"]);
        Assert.Equal("Reinhard", (string?)node["tonemapper"]);
        Assert.Equal(2.5f, (float?)node["exposure"]);

        var loaded = new WorldEnvironmentSettings();
        loaded.Load(saved);
        AssertNonDefault(loaded);
    }

    [Theory]
    [InlineData("Reinhard", 1.0f)]
    [InlineData("AcesApprox", 3.0f)]
    public void Save_WritesTheKeyWhenOnlyOneFieldDiffers(string tonemapper, float exposure)
    {
        var settings = new WorldEnvironmentSettings();
        settings.ColorPipeline.Tonemapper = Enum.Parse<TonemapOperator>(tonemapper);
        settings.ColorPipeline.Exposure = exposure;
        var saved = new JObject();

        settings.Save(saved);

        Assert.True(saved.ContainsKey("color_pipeline"));
    }

    [Fact]
    public void Save_RemovesAStaleKeyWhenBackToDefault()
    {
        var saved = new JObject { ["color_pipeline"] = new JObject { ["mode"] = "LinearHdr" } };

        new WorldEnvironmentSettings().Save(saved);

        Assert.False(saved.ContainsKey("color_pipeline"));
    }

    [Fact]
    public void Load_TreatsAnAbsentKeyAsLegacy_EvenOnSettingsThatWereLinear()
    {
        var settings = CreateNonDefault();

        settings.Load(new JObject());

        Assert.True(settings.ColorPipeline.IsDefault);
    }

    [Fact]
    public void Load_ExistingWorldWithoutTheKeyLoadsAsLegacy()
    {
        var saved = new JObject();
        new WorldEnvironmentSettings().Save(saved);
        var loaded = new WorldEnvironmentSettings();

        loaded.Load(saved);

        Assert.Equal(ColorPipelineMode.Legacy, loaded.ColorPipeline.Mode);
    }

    [Fact]
    public void Load_ReadsTheKeysCaseInsensitively()
    {
        var settings = new WorldEnvironmentSettings();

        settings.Load(new JObject
        {
            ["COLOR_PIPELINE"] = new JObject { ["Mode"] = "linearhdr", ["TONEMAPPER"] = "reinhard", ["Exposure"] = 0.5f },
        });

        Assert.Equal(ColorPipelineMode.LinearHdr, settings.ColorPipeline.Mode);
        Assert.Equal(TonemapOperator.Reinhard, settings.ColorPipeline.Tonemapper);
        Assert.Equal(0.5f, settings.ColorPipeline.Exposure);
    }

    [Fact]
    public void ResetToDefaults_ReturnsToLegacy()
    {
        var settings = CreateNonDefault();

        settings.ResetToDefaults();

        Assert.True(settings.ColorPipeline.IsDefault);
        Assert.Equal(ColorPipelineMode.Legacy, settings.ColorPipeline.Mode);
    }

    [Fact]
    public void EnvironmentResolver_CarriesTheColorPipelineOfTheWorldToTheResolvedSettings()
    {
        var world = new World();
        ApplyNonDefault(world.EnvironmentSettings);
        var view = new RenderView(world, new ArcBallCameraComponent(), new StubRenderSurface());

        var resolved = EnvironmentResolver.Resolve(view);

        Assert.Equal(ColorPipelineMode.LinearHdr, resolved.ColorPipelineMode);
        Assert.Equal(TonemapOperator.Reinhard, resolved.Tonemapper);
        Assert.Equal(2.5f, resolved.Exposure);
        Assert.True(resolved.IsLinearHdr);
    }

    [Fact]
    public void EnvironmentResolver_ResolvesLegacyByDefault()
    {
        var view = new RenderView(new World(), new ArcBallCameraComponent(), new StubRenderSurface());

        var resolved = EnvironmentResolver.Resolve(view);

        Assert.Equal(ColorPipelineMode.Legacy, resolved.ColorPipelineMode);
        Assert.Equal(1.0f, resolved.Exposure);
        Assert.False(resolved.IsLinearHdr);
    }

    [Fact]
    public void EnvironmentResolver_UsesTheViewOverrideColorPipeline()
    {
        var world = new World();
        var view = new RenderView(world, new ArcBallCameraComponent(), new StubRenderSurface())
        {
            EnvironmentOverride = CreateNonDefault(),
        };

        var resolved = EnvironmentResolver.Resolve(view);

        Assert.True(resolved.IsLinearHdr);
        Assert.Equal(TonemapOperator.Reinhard, resolved.Tonemapper);
        Assert.Equal(2.5f, resolved.Exposure);
    }

    [Fact]
    public void EnvironmentResolver_ReturnsTheNewValueAfterAChangeFollowedByMarkDirty()
    {
        var world = new World();
        var view = new RenderView(world, new ArcBallCameraComponent(), new StubRenderSurface());
        Assert.False(EnvironmentResolver.Resolve(view).IsLinearHdr);

        world.EnvironmentSettings.ColorPipeline.Mode = ColorPipelineMode.LinearHdr;
        world.EnvironmentSettings.ColorPipeline.Exposure = 4.0f;
        world.EnvironmentSettings.MarkDirty();

        var resolved = EnvironmentResolver.Resolve(view);
        Assert.True(resolved.IsLinearHdr);
        Assert.Equal(4.0f, resolved.Exposure);
    }

    [Fact]
    public void ResolvedEnvironmentSettings_LegacyIsTheDefaultAndTheCreateLegacyValue()
    {
        Assert.False(default(ResolvedEnvironmentSettings).IsLinearHdr);

        var legacy = ResolvedEnvironmentSettings.CreateLegacy(Color.Black, Vector3.Zero);
        Assert.False(legacy.IsLinearHdr);
        Assert.Equal(1.0f, legacy.Exposure);
    }

    private static WorldEnvironmentSettings CreateNonDefault()
    {
        var settings = new WorldEnvironmentSettings();
        ApplyNonDefault(settings);
        return settings;
    }

    private static void ApplyNonDefault(WorldEnvironmentSettings settings)
    {
        settings.ColorPipeline.Mode = ColorPipelineMode.LinearHdr;
        settings.ColorPipeline.Tonemapper = TonemapOperator.Reinhard;
        settings.ColorPipeline.Exposure = 2.5f;
    }

    private static void AssertNonDefault(WorldEnvironmentSettings settings)
    {
        Assert.Equal(ColorPipelineMode.LinearHdr, settings.ColorPipeline.Mode);
        Assert.Equal(TonemapOperator.Reinhard, settings.ColorPipeline.Tonemapper);
        Assert.Equal(2.5f, settings.ColorPipeline.Exposure);
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
