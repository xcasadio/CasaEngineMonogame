using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class ProceduralSkySettingsJsonSerializer
{
    public static JObject Save(ProceduralSkySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new JObject
        {
            ["zenith_color"] = SaveColor(settings.ZenithColor),
            ["horizon_color"] = SaveColor(settings.HorizonColor),
            ["ground_color"] = SaveColor(settings.GroundColor),
            ["sky_exponent"] = settings.SkyExponent,
            ["ground_exponent"] = settings.GroundExponent,
            ["cubemap_size"] = settings.CubemapSize,
        };
    }

    public static ProceduralSkySettings Load(JObject? element)
    {
        var settings = new ProceduralSkySettings();
        if (element is null)
        {
            return settings;
        }

        if (element.TryGetValue("zenith_color", StringComparison.OrdinalIgnoreCase, out var zenithColorNode))
        {
            settings.ZenithColor = zenithColorNode.GetColor();
        }

        if (element.TryGetValue("horizon_color", StringComparison.OrdinalIgnoreCase, out var horizonColorNode))
        {
            settings.HorizonColor = horizonColorNode.GetColor();
        }

        if (element.TryGetValue("ground_color", StringComparison.OrdinalIgnoreCase, out var groundColorNode))
        {
            settings.GroundColor = groundColorNode.GetColor();
        }

        if (element.TryGetValue("sky_exponent", StringComparison.OrdinalIgnoreCase, out var skyExponentNode))
        {
            settings.SkyExponent = skyExponentNode.GetSingle();
        }

        if (element.TryGetValue("ground_exponent", StringComparison.OrdinalIgnoreCase, out var groundExponentNode))
        {
            settings.GroundExponent = groundExponentNode.GetSingle();
        }

        if (element.TryGetValue("cubemap_size", StringComparison.OrdinalIgnoreCase, out var cubemapSizeNode))
        {
            settings.CubemapSize = ProceduralSkyEnvironmentGenerator.NormalizeCubemapSize(cubemapSizeNode.GetInt32());
        }

        return settings;
    }

    private static JObject SaveColor(Color value)
    {
        return new JObject
        {
            ["r"] = value.R,
            ["g"] = value.G,
            ["b"] = value.B,
            ["a"] = value.A,
        };
    }
}