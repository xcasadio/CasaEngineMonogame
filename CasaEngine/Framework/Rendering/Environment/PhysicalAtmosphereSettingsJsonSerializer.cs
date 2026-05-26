using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class PhysicalAtmosphereSettingsJsonSerializer
{
    public static JObject Save(PhysicalAtmosphereSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new JObject
        {
            ["sun_direction"] = SaveVector3(settings.SunDirection),
            ["space_color"] = SaveColor(settings.SpaceColor),
            ["rayleigh_color"] = SaveColor(settings.RayleighColor),
            ["mie_color"] = SaveColor(settings.MieColor),
            ["sunset_color"] = SaveColor(settings.SunsetColor),
            ["ground_color"] = SaveColor(settings.GroundColor),
            ["atmosphere_density"] = settings.AtmosphereDensity,
            ["rayleigh_density"] = settings.RayleighDensity,
            ["mie_density"] = settings.MieDensity,
            ["mie_anisotropy"] = settings.MieAnisotropy,
            ["sun_intensity"] = settings.SunIntensity,
            ["sun_disc_size"] = settings.SunDiscSize,
            ["ground_falloff"] = settings.GroundFalloff,
            ["space_falloff"] = settings.SpaceFalloff,
            ["cubemap_size"] = settings.CubemapSize,
        };
    }

    public static PhysicalAtmosphereSettings Load(JObject element)
    {
        var settings = new PhysicalAtmosphereSettings();
        if (element is null)
        {
            return settings;
        }

        if (element.TryGetValue("sun_direction", StringComparison.OrdinalIgnoreCase, out var sunDirectionNode))
        {
            settings.SunDirection = sunDirectionNode.GetVector3();
        }

        if (element.TryGetValue("space_color", StringComparison.OrdinalIgnoreCase, out var spaceColorNode))
        {
            settings.SpaceColor = spaceColorNode.GetColor();
        }

        if (element.TryGetValue("rayleigh_color", StringComparison.OrdinalIgnoreCase, out var rayleighColorNode))
        {
            settings.RayleighColor = rayleighColorNode.GetColor();
        }

        if (element.TryGetValue("mie_color", StringComparison.OrdinalIgnoreCase, out var mieColorNode))
        {
            settings.MieColor = mieColorNode.GetColor();
        }

        if (element.TryGetValue("sunset_color", StringComparison.OrdinalIgnoreCase, out var sunsetColorNode))
        {
            settings.SunsetColor = sunsetColorNode.GetColor();
        }

        if (element.TryGetValue("ground_color", StringComparison.OrdinalIgnoreCase, out var groundColorNode))
        {
            settings.GroundColor = groundColorNode.GetColor();
        }

        if (element.TryGetValue("atmosphere_density", StringComparison.OrdinalIgnoreCase, out var atmosphereDensityNode))
        {
            settings.AtmosphereDensity = atmosphereDensityNode.GetSingle();
        }

        if (element.TryGetValue("rayleigh_density", StringComparison.OrdinalIgnoreCase, out var rayleighDensityNode))
        {
            settings.RayleighDensity = rayleighDensityNode.GetSingle();
        }

        if (element.TryGetValue("mie_density", StringComparison.OrdinalIgnoreCase, out var mieDensityNode))
        {
            settings.MieDensity = mieDensityNode.GetSingle();
        }

        if (element.TryGetValue("mie_anisotropy", StringComparison.OrdinalIgnoreCase, out var mieAnisotropyNode))
        {
            settings.MieAnisotropy = mieAnisotropyNode.GetSingle();
        }

        if (element.TryGetValue("sun_intensity", StringComparison.OrdinalIgnoreCase, out var sunIntensityNode))
        {
            settings.SunIntensity = sunIntensityNode.GetSingle();
        }

        if (element.TryGetValue("sun_disc_size", StringComparison.OrdinalIgnoreCase, out var sunDiscSizeNode))
        {
            settings.SunDiscSize = sunDiscSizeNode.GetSingle();
        }

        if (element.TryGetValue("ground_falloff", StringComparison.OrdinalIgnoreCase, out var groundFalloffNode))
        {
            settings.GroundFalloff = groundFalloffNode.GetSingle();
        }

        if (element.TryGetValue("space_falloff", StringComparison.OrdinalIgnoreCase, out var spaceFalloffNode))
        {
            settings.SpaceFalloff = spaceFalloffNode.GetSingle();
        }

        if (element.TryGetValue("cubemap_size", StringComparison.OrdinalIgnoreCase, out var cubemapSizeNode))
        {
            settings.CubemapSize = PhysicalAtmosphereEnvironmentGenerator.NormalizeCubemapSize(cubemapSizeNode.GetInt32());
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

    private static JObject SaveVector3(Vector3 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };
    }
}