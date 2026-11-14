using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shadows;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShadowSettingsTests
{
    [Fact]
    public void Defaults_KeepTheHistoricalBehaviour()
    {
        var settings = new ShadowSettings();

        Assert.Equal(ShadowMode.OccludeDirectLight, settings.Mode);
        Assert.Equal(Vector3.Zero, settings.ShadowColor);
    }

    [Fact]
    public void ShadowColor_IsKeptInTheUnitRangeAndNonFiniteComponentsBecomeZero()
    {
        var settings = new ShadowSettings
        {
            ShadowColor = new Vector3(-0.5f, 2.0f, float.NaN),
        };

        Assert.Equal(new Vector3(0.0f, 1.0f, 0.0f), settings.ShadowColor);

        settings.ShadowColor = new Vector3(float.PositiveInfinity, 0.25f, float.NegativeInfinity);

        Assert.Equal(new Vector3(0.0f, 0.25f, 0.0f), settings.ShadowColor);
    }

    [Fact]
    public void Save_OmitsModeAndShadowColorWhenDefault_SoExistingWorldsKeepTheirFormat()
    {
        var saved = new JObject();

        new ShadowSettings().Save(saved);

        Assert.False(saved.ContainsKey("mode"));
        Assert.False(saved.ContainsKey("shadow_color"));
        Assert.True(saved.ContainsKey("max_distance"));
    }

    [Fact]
    public void Save_RemovesStaleKeysWhenBackToDefault()
    {
        var saved = new JObject { ["mode"] = "MultiplyLitColor", ["shadow_color"] = new JObject { ["x"] = 1, ["y"] = 1, ["z"] = 1 } };

        new ShadowSettings().Save(saved);

        Assert.False(saved.ContainsKey("mode"));
        Assert.False(saved.ContainsKey("shadow_color"));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsModeAndShadowColor()
    {
        var original = new ShadowSettings
        {
            Enabled = true,
            Mode = ShadowMode.MultiplyLitColor,
            ShadowColor = new Vector3(0.25f, 0.26f, 0.27f),
        };
        var saved = new JObject();
        original.Save(saved);

        Assert.Equal("MultiplyLitColor", (string?)saved["mode"]);

        var loaded = new ShadowSettings();
        loaded.Load(saved);

        Assert.Equal(ShadowMode.MultiplyLitColor, loaded.Mode);
        Assert.Equal(new Vector3(0.25f, 0.26f, 0.27f), loaded.ShadowColor);
        Assert.True(loaded.Enabled);
    }

    [Fact]
    public void Load_TreatsAbsentFieldsAsDefaults_EvenOnSettingsThatWereCustomised()
    {
        var settings = new ShadowSettings { Mode = ShadowMode.MultiplyLitColor, ShadowColor = Vector3.One };

        settings.Load(new JObject { ["enabled"] = true });

        Assert.Equal(ShadowMode.OccludeDirectLight, settings.Mode);
        Assert.Equal(Vector3.Zero, settings.ShadowColor);
    }

    [Fact]
    public void Load_ReadsTheModeCaseInsensitively()
    {
        var settings = new ShadowSettings();

        settings.Load(new JObject { ["mode"] = "multiplylitcolor" });

        Assert.Equal(ShadowMode.MultiplyLitColor, settings.Mode);
    }

    [Fact]
    public void CopyFrom_CopiesModeAndShadowColorBothWays()
    {
        var customised = new ShadowSettings { Mode = ShadowMode.MultiplyLitColor, ShadowColor = new Vector3(0.1f, 0.2f, 0.3f) };
        var target = new ShadowSettings();

        target.CopyFrom(customised);
        Assert.Equal(ShadowMode.MultiplyLitColor, target.Mode);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), target.ShadowColor);

        target.CopyFrom(new ShadowSettings());
        Assert.Equal(ShadowMode.OccludeDirectLight, target.Mode);
        Assert.Equal(Vector3.Zero, target.ShadowColor);
    }

    [Fact]
    public void ResetToDefaults_RestoresModeAndShadowColor()
    {
        var settings = new ShadowSettings { Mode = ShadowMode.MultiplyLitColor, ShadowColor = Vector3.One };

        settings.ResetToDefaults();

        Assert.Equal(ShadowMode.OccludeDirectLight, settings.Mode);
        Assert.Equal(Vector3.Zero, settings.ShadowColor);
    }

    [Fact]
    public void WorldEnvironmentSettings_CarriesTheShadowStyleThroughSaveLoadCloneAndCopy()
    {
        var world = new WorldEnvironmentSettings();
        world.Shadows.Mode = ShadowMode.MultiplyLitColor;
        world.Shadows.ShadowColor = new Vector3(0.25f, 0.26f, 0.27f);

        var saved = new JObject();
        world.Save(saved);
        var loaded = new WorldEnvironmentSettings();
        loaded.Load(saved);

        Assert.Equal(ShadowMode.MultiplyLitColor, loaded.Shadows.Mode);
        Assert.Equal(new Vector3(0.25f, 0.26f, 0.27f), loaded.Shadows.ShadowColor);
        Assert.Equal(ShadowMode.MultiplyLitColor, world.Clone().Shadows.Mode);

        var copy = new WorldEnvironmentSettings();
        copy.CopyFrom(world);
        Assert.Equal(new Vector3(0.25f, 0.26f, 0.27f), copy.Shadows.ShadowColor);
    }
}
