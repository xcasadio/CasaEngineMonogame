using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class EnvironmentLightingSanitizationTests
{
    [Fact]
    public void WorldEnvironmentSettings_NormalizeAmbientLightingInputs()
    {
        var settings = new WorldEnvironmentSettings
        {
            AmbientColor = new Vector3(-0.5f, 0.25f, 1.5f),
            AmbientIntensity = -2.0f,
            SpecularIntensity = float.NaN,
        };

        Assert.Equal(new Vector3(0.0f, 0.25f, 1.5f), settings.AmbientColor);
        Assert.Equal(0.0f, settings.AmbientIntensity);
        Assert.Equal(0.0f, settings.SpecularIntensity);

        settings.Load(new JObject
        {
            ["ambient_color"] = new JObject
            {
                ["x"] = -1.0f,
                ["y"] = 0.4f,
                ["z"] = -0.2f,
            },
            ["ambient_intensity"] = -3.0f,
            ["specular_intensity"] = -4.0f,
        });

        Assert.Equal(new Vector3(0.0f, 0.4f, 0.0f), settings.AmbientColor);
        Assert.Equal(0.0f, settings.AmbientIntensity);
        Assert.Equal(0.0f, settings.SpecularIntensity);

        var saved = new JObject();
        settings.Save(saved);

        var ambientNode = Assert.IsType<JObject>(saved["ambient_color"]);
        Assert.Equal(0.0f, (float?)ambientNode["x"]);
        Assert.Equal(0.4f, (float?)ambientNode["y"]);
        Assert.Equal(0.0f, (float?)ambientNode["z"]);
        Assert.Equal(0.0f, (float?)saved["ambient_intensity"]);
        Assert.Equal(0.0f, (float?)saved["specular_intensity"]);
    }

    [Fact]
    public void EnvironmentAsset_NormalizeAmbientLightingInputs()
    {
        var asset = new EnvironmentAsset
        {
            AmbientColor = new Vector3(0.3f, -0.1f, 2.0f),
            AmbientIntensity = -1.0f,
            SpecularIntensity = float.NaN,
        };

        Assert.Equal(new Vector3(0.3f, 0.0f, 2.0f), asset.AmbientColor);
        Assert.Equal(0.0f, asset.AmbientIntensity);
        Assert.Equal(0.0f, asset.SpecularIntensity);

        EnvironmentAssetJsonSerializer.Load(asset, new JObject
        {
            ["ambient_color"] = new JObject
            {
                ["x"] = -0.25f,
                ["y"] = 0.6f,
                ["z"] = -0.75f,
            },
            ["ambient_intensity"] = -5.0f,
            ["specular_intensity"] = -6.0f,
        });

        Assert.Equal(new Vector3(0.0f, 0.6f, 0.0f), asset.AmbientColor);
        Assert.Equal(0.0f, asset.AmbientIntensity);
        Assert.Equal(0.0f, asset.SpecularIntensity);

        var saved = new JObject();
        EnvironmentAssetJsonSerializer.Save(asset, saved);

        var ambientNode = Assert.IsType<JObject>(saved["ambient_color"]);
        Assert.Equal(0.0f, (float?)ambientNode["x"]);
        Assert.Equal(0.6f, (float?)ambientNode["y"]);
        Assert.Equal(0.0f, (float?)ambientNode["z"]);
        Assert.Equal(0.0f, (float?)saved["ambient_intensity"]);
        Assert.Equal(0.0f, (float?)saved["specular_intensity"]);
    }

    [Fact]
    public void ResolveAmbientColor_UsesWorldTintForEnvironmentAssets()
    {
        var worldSettings = new WorldEnvironmentSettings
        {
            AmbientColor = new Vector3(0.10f, 0.025f, 0.05f),
        };
        var environmentAsset = new EnvironmentAsset
        {
            AmbientColor = new Vector3(0.20f, 0.40f, 0.80f),
        };

        Vector3 tintedColor = EnvironmentResolver.ResolveAmbientColor(worldSettings, environmentAsset);

        Assert.Equal(new Vector3(0.40f, 0.20f, 0.80f), tintedColor);

        worldSettings.AmbientColor = EnvironmentResolver.LegacyAmbientColor;
        Vector3 untintedColor = EnvironmentResolver.ResolveAmbientColor(worldSettings, environmentAsset);

        Assert.Equal(environmentAsset.AmbientColor, untintedColor);
    }
}