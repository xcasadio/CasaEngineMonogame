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
        Assert.Equal(AntiAliasingMode.Fxaa, resolved.AntiAliasing);
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
        Assert.Equal(AntiAliasingMode.Fxaa, resolved.AntiAliasing);
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
        Assert.Equal(AntiAliasingMode.None, legacy.AntiAliasing);
    }

    [Fact]
    public void Bloom_DefaultsAreOffWithThresholdOneIntensityHalfRadiusOne()
    {
        var settings = new WorldEnvironmentSettings().ColorPipeline;

        Assert.False(settings.BloomEnabled);
        Assert.Equal(1.0f, settings.BloomThreshold);
        Assert.Equal(0.5f, settings.BloomIntensity);
        Assert.Equal(1.0f, settings.BloomRadius);
    }

    [Fact]
    public void Bloom_NaNInfinityAndNegativeValuesBecomeZero()
    {
        var settings = new ColorPipelineSettings
        {
            BloomThreshold = float.NaN,
            BloomIntensity = -1.0f,
            BloomRadius = float.PositiveInfinity,
        };

        Assert.Equal(0.0f, settings.BloomThreshold);
        Assert.Equal(0.0f, settings.BloomIntensity);
        Assert.Equal(0.0f, settings.BloomRadius);

        settings.BloomThreshold = 2.0f;
        settings.BloomIntensity = 0.25f;
        settings.BloomRadius = 3.0f;
        Assert.Equal(2.0f, settings.BloomThreshold);
        Assert.Equal(0.25f, settings.BloomIntensity);
        Assert.Equal(3.0f, settings.BloomRadius);
    }

    [Fact]
    public void Bloom_SaveWritesTheKeysAdditivelyAndLoadReadsThemBack()
    {
        var saved = new JObject();
        CreateNonDefault().Save(saved);

        var node = Assert.IsType<JObject>(saved["color_pipeline"]);
        Assert.True((bool?)node["bloom_enabled"]);
        Assert.Equal(2.0f, (float?)node["bloom_threshold"]);
        Assert.Equal(0.25f, (float?)node["bloom_intensity"]);
        Assert.Equal(3.0f, (float?)node["bloom_radius"]);

        var loaded = new WorldEnvironmentSettings();
        loaded.Load(saved);
        AssertNonDefault(loaded);
    }

    [Fact]
    public void Bloom_LoadOfAColourPipelineWrittenBeforeBloomKeepsTheBloomDefaults()
    {
        var settings = new WorldEnvironmentSettings();

        settings.Load(new JObject
        {
            ["color_pipeline"] = new JObject { ["mode"] = "LinearHdr", ["tonemapper"] = "Reinhard", ["exposure"] = 2.0f },
        });

        Assert.Equal(ColorPipelineMode.LinearHdr, settings.ColorPipeline.Mode);
        Assert.False(settings.ColorPipeline.BloomEnabled);
        Assert.Equal(1.0f, settings.ColorPipeline.BloomThreshold);
        Assert.Equal(0.5f, settings.ColorPipeline.BloomIntensity);
        Assert.Equal(1.0f, settings.ColorPipeline.BloomRadius);
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("threshold")]
    [InlineData("intensity")]
    [InlineData("radius")]
    public void Bloom_SaveWritesTheKeyWhenOnlyOneBloomFieldDiffers(string field)
    {
        var settings = new WorldEnvironmentSettings();
        switch (field)
        {
            case "enabled": settings.ColorPipeline.BloomEnabled = true; break;
            case "threshold": settings.ColorPipeline.BloomThreshold = 2.0f; break;
            case "intensity": settings.ColorPipeline.BloomIntensity = 0.75f; break;
            default: settings.ColorPipeline.BloomRadius = 2.0f; break;
        }

        var saved = new JObject();
        settings.Save(saved);

        Assert.True(saved.ContainsKey("color_pipeline"));
        Assert.False(settings.ColorPipeline.IsDefault);
    }

    [Fact]
    public void Bloom_LoadReadsTheKeysCaseInsensitively()
    {
        var settings = new WorldEnvironmentSettings();

        settings.Load(new JObject
        {
            ["color_pipeline"] = new JObject
            {
                ["BLOOM_ENABLED"] = true,
                ["Bloom_Threshold"] = 3.0f,
                ["bloom_INTENSITY"] = 0.1f,
                ["Bloom_Radius"] = 2.0f,
            },
        });

        Assert.True(settings.ColorPipeline.BloomEnabled);
        Assert.Equal(3.0f, settings.ColorPipeline.BloomThreshold);
        Assert.Equal(0.1f, settings.ColorPipeline.BloomIntensity);
        Assert.Equal(2.0f, settings.ColorPipeline.BloomRadius);
    }

    [Fact]
    public void Bloom_ResolverCarriesTheValuesAndTheViewOverrideWins()
    {
        var world = new World();
        ApplyNonDefault(world.EnvironmentSettings);
        var view = new RenderView(world, new ArcBallCameraComponent(), new StubRenderSurface());

        var resolved = EnvironmentResolver.Resolve(view);

        Assert.True(resolved.BloomEnabled);
        Assert.Equal(2.0f, resolved.BloomThreshold);
        Assert.Equal(0.25f, resolved.BloomIntensity);
        Assert.Equal(3.0f, resolved.BloomRadius);

        var overridden = new RenderView(new World(), new ArcBallCameraComponent(), new StubRenderSurface())
        {
            EnvironmentOverride = CreateNonDefault(),
        };
        Assert.True(EnvironmentResolver.Resolve(overridden).BloomEnabled);
    }

    [Fact]
    public void Bloom_ResolverIsOffByDefaultAndFollowsAChangeFollowedByMarkDirty()
    {
        var world = new World();
        var view = new RenderView(world, new ArcBallCameraComponent(), new StubRenderSurface());
        Assert.False(EnvironmentResolver.Resolve(view).BloomEnabled);

        world.EnvironmentSettings.ColorPipeline.BloomEnabled = true;
        world.EnvironmentSettings.ColorPipeline.BloomIntensity = 0.9f;
        world.EnvironmentSettings.MarkDirty();

        var resolved = EnvironmentResolver.Resolve(view);
        Assert.True(resolved.BloomEnabled);
        Assert.Equal(0.9f, resolved.BloomIntensity);
    }

    [Fact]
    public void Bloom_CreateLegacyHasBloomOffAndTheDefaultValues()
    {
        var legacy = ResolvedEnvironmentSettings.CreateLegacy(Color.Black, Vector3.Zero);

        Assert.False(legacy.BloomEnabled);
        Assert.Equal(ColorPipelineSettings.DefaultBloomThreshold, legacy.BloomThreshold);
        Assert.Equal(ColorPipelineSettings.DefaultBloomIntensity, legacy.BloomIntensity);
        Assert.Equal(ColorPipelineSettings.DefaultBloomRadius, legacy.BloomRadius);
        Assert.False(default(ResolvedEnvironmentSettings).BloomEnabled);
    }

    [Fact]
    public void Bloom_CopyFromAndCloneCarryTheBloomFields()
    {
        var target = new WorldEnvironmentSettings();
        target.CopyFrom(CreateNonDefault());
        AssertNonDefault(target);

        target.CopyFrom(new WorldEnvironmentSettings());
        Assert.True(target.ColorPipeline.IsDefault);

        AssertNonDefault(CreateNonDefault().Clone());
    }

    [Fact]
    public void AntiAliasing_DefaultsToNoneAndIsPartOfIsDefault()
    {
        var settings = new ColorPipelineSettings();

        Assert.Equal(AntiAliasingMode.None, settings.AntiAliasing);
        Assert.True(settings.IsDefault);

        settings.AntiAliasing = AntiAliasingMode.Fxaa;
        Assert.False(settings.IsDefault);

        settings.ResetToDefaults();
        Assert.Equal(AntiAliasingMode.None, settings.AntiAliasing);
        Assert.True(settings.IsDefault);
    }

    [Fact]
    public void AntiAliasing_IsSavedByNameAndReadBackCaseInsensitively()
    {
        var saved = new JObject();
        CreateNonDefault().Save(saved);

        var node = Assert.IsType<JObject>(saved["color_pipeline"]);
        Assert.Equal("Fxaa", (string?)node["anti_aliasing"]);

        var loaded = new WorldEnvironmentSettings();
        loaded.Load(new JObject { ["color_pipeline"] = new JObject { ["ANTI_ALIASING"] = "fxaa" } });
        Assert.Equal(AntiAliasingMode.Fxaa, loaded.ColorPipeline.AntiAliasing);
    }

    [Fact]
    public void AntiAliasing_LoadOfAColourPipelineWrittenBeforeTheKeyReadsNone()
    {
        var settings = new WorldEnvironmentSettings();
        settings.ColorPipeline.AntiAliasing = AntiAliasingMode.Fxaa;

        settings.Load(new JObject
        {
            ["color_pipeline"] = new JObject { ["mode"] = "LinearHdr", ["tonemapper"] = "Reinhard", ["exposure"] = 2.0f },
        });

        Assert.Equal(ColorPipelineMode.LinearHdr, settings.ColorPipeline.Mode);
        Assert.Equal(AntiAliasingMode.None, settings.ColorPipeline.AntiAliasing);
    }

    [Fact]
    public void AntiAliasing_WhenOnlyThatFieldDiffersTheKeyIsWritten_AndBackToNoneOmitsColorPipeline()
    {
        var settings = new WorldEnvironmentSettings();
        settings.ColorPipeline.AntiAliasing = AntiAliasingMode.Fxaa;
        var saved = new JObject();

        settings.Save(saved);
        Assert.True(saved.ContainsKey("color_pipeline"));

        settings.ColorPipeline.AntiAliasing = AntiAliasingMode.None;
        settings.Save(saved);
        Assert.False(saved.ContainsKey("color_pipeline"));
    }

    [Fact]
    public void Ssao_DefaultsToOffWithTheDecidedValues_AndIsPartOfIsDefault()
    {
        var settings = new ColorPipelineSettings();

        Assert.False(settings.SsaoEnabled);
        Assert.Equal(0.5f, settings.SsaoRadius);
        Assert.Equal(1.0f, settings.SsaoIntensity);
        Assert.Equal(0.02f, settings.SsaoBias);
        Assert.True(settings.IsDefault);

        foreach (var change in new Action<ColorPipelineSettings>[]
                 {
                     s => s.SsaoEnabled = true,
                     s => s.SsaoRadius = 1.0f,
                     s => s.SsaoIntensity = 2.0f,
                     s => s.SsaoBias = 0.0f,
                 })
        {
            var changed = new ColorPipelineSettings();
            change(changed);
            Assert.False(changed.IsDefault);

            changed.ResetToDefaults();
            Assert.True(changed.IsDefault);
        }
    }

    [Fact]
    public void Ssao_NaNInfinityAndNegativeValuesBecomeZero()
    {
        var settings = new ColorPipelineSettings { SsaoRadius = float.NaN, SsaoIntensity = -1.0f, SsaoBias = float.PositiveInfinity };

        Assert.Equal(0.0f, settings.SsaoRadius);
        Assert.Equal(0.0f, settings.SsaoIntensity);
        Assert.Equal(0.0f, settings.SsaoBias);
    }

    [Fact]
    public void Ssao_IsSavedUnderItsKeysAndReadBackCaseInsensitively()
    {
        var saved = new JObject();
        CreateNonDefault().Save(saved);

        var node = Assert.IsType<JObject>(saved["color_pipeline"]);
        Assert.True((bool?)node["ssao_enabled"]);
        Assert.Equal(1.5f, (float?)node["ssao_radius"]);
        Assert.Equal(0.75f, (float?)node["ssao_intensity"]);
        Assert.Equal(0.1f, (float?)node["ssao_bias"]);

        var loaded = new WorldEnvironmentSettings();
        loaded.Load(new JObject
        {
            ["color_pipeline"] = new JObject { ["SSAO_ENABLED"] = true, ["Ssao_Radius"] = 2.0f, ["ssao_intensity"] = 0.5f, ["ssao_bias"] = 0.05f },
        });
        Assert.True(loaded.ColorPipeline.SsaoEnabled);
        Assert.Equal(2.0f, loaded.ColorPipeline.SsaoRadius);
        Assert.Equal(0.5f, loaded.ColorPipeline.SsaoIntensity);
        Assert.Equal(0.05f, loaded.ColorPipeline.SsaoBias);
    }

    [Fact]
    public void Ssao_LoadOfAColourPipelineWrittenBeforeTheKeysReadsTheDefaults()
    {
        var settings = new WorldEnvironmentSettings();
        settings.ColorPipeline.SsaoEnabled = true;
        settings.ColorPipeline.SsaoRadius = 4.0f;

        settings.Load(new JObject
        {
            ["color_pipeline"] = new JObject { ["mode"] = "LinearHdr", ["anti_aliasing"] = "Fxaa" },
        });

        Assert.Equal(ColorPipelineMode.LinearHdr, settings.ColorPipeline.Mode);
        Assert.False(settings.ColorPipeline.SsaoEnabled);
        Assert.Equal(ColorPipelineSettings.DefaultSsaoRadius, settings.ColorPipeline.SsaoRadius);
        Assert.Equal(ColorPipelineSettings.DefaultSsaoIntensity, settings.ColorPipeline.SsaoIntensity);
        Assert.Equal(ColorPipelineSettings.DefaultSsaoBias, settings.ColorPipeline.SsaoBias);
    }

    [Fact]
    public void Ssao_WhenOnlyThatFieldDiffersTheKeysAreWritten_AndBackToDefaultOmitsColorPipeline()
    {
        var settings = new WorldEnvironmentSettings();
        settings.ColorPipeline.SsaoEnabled = true;
        var saved = new JObject();

        settings.Save(saved);
        Assert.True(saved.ContainsKey("color_pipeline"));

        settings.ColorPipeline.SsaoEnabled = false;
        settings.Save(saved);
        Assert.False(saved.ContainsKey("color_pipeline"));
    }

    [Fact]
    public void Ssao_ReachesTheResolvedEnvironment_AndIsOffInTheLegacyOne()
    {
        var world = new World();
        world.EnvironmentSettings.ColorPipeline.SsaoEnabled = true;
        world.EnvironmentSettings.ColorPipeline.SsaoRadius = 1.25f;
        world.EnvironmentSettings.ColorPipeline.SsaoIntensity = 0.5f;
        world.EnvironmentSettings.ColorPipeline.SsaoBias = 0.07f;
        world.EnvironmentSettings.MarkDirty();

        var view = new RenderView(world, new ArcBallCameraComponent(), new StubRenderSurface());
        var resolved = EnvironmentResolver.Resolve(view);

        Assert.True(resolved.SsaoEnabled);
        Assert.Equal(1.25f, resolved.SsaoRadius);
        Assert.Equal(0.5f, resolved.SsaoIntensity);
        Assert.Equal(0.07f, resolved.SsaoBias);
        Assert.False(ResolvedEnvironmentSettings.CreateLegacy(Color.Black, Vector3.Zero).SsaoEnabled);
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
        settings.ColorPipeline.BloomEnabled = true;
        settings.ColorPipeline.BloomThreshold = 2.0f;
        settings.ColorPipeline.BloomIntensity = 0.25f;
        settings.ColorPipeline.BloomRadius = 3.0f;
        settings.ColorPipeline.AntiAliasing = AntiAliasingMode.Fxaa;
        settings.ColorPipeline.SsaoEnabled = true;
        settings.ColorPipeline.SsaoRadius = 1.5f;
        settings.ColorPipeline.SsaoIntensity = 0.75f;
        settings.ColorPipeline.SsaoBias = 0.1f;
    }

    private static void AssertNonDefault(WorldEnvironmentSettings settings)
    {
        Assert.Equal(ColorPipelineMode.LinearHdr, settings.ColorPipeline.Mode);
        Assert.Equal(TonemapOperator.Reinhard, settings.ColorPipeline.Tonemapper);
        Assert.Equal(2.5f, settings.ColorPipeline.Exposure);
        Assert.True(settings.ColorPipeline.BloomEnabled);
        Assert.Equal(2.0f, settings.ColorPipeline.BloomThreshold);
        Assert.Equal(0.25f, settings.ColorPipeline.BloomIntensity);
        Assert.Equal(3.0f, settings.ColorPipeline.BloomRadius);
        Assert.Equal(AntiAliasingMode.Fxaa, settings.ColorPipeline.AntiAliasing);
        Assert.True(settings.ColorPipeline.SsaoEnabled);
        Assert.Equal(1.5f, settings.ColorPipeline.SsaoRadius);
        Assert.Equal(0.75f, settings.ColorPipeline.SsaoIntensity);
        Assert.Equal(0.1f, settings.ColorPipeline.SsaoBias);
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
