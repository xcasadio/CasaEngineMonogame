using System;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos;

internal static class DemoSceneLightRig
{
    public static void AddLegacyDirectionalRig(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        AddDirectionalLight(
            world,
            "LegacyKeyLight",
            new Vector3(-0.5265408f, -0.5735765f, -0.6275069f),
            new Color(0.92f, 0.92f, 0.92f),
            new Color(0.92f, 0.92f, 0.92f),
            1.0f,
            castShadows: true);

        AddDirectionalLight(
            world,
            "LegacyFillLight",
            new Vector3(0.7198464f, 0.3420201f, 0.6040227f),
            new Color(0.71f, 0.71f, 0.71f),
            Color.Black,
            1.0f);

        AddDirectionalLight(
            world,
            "LegacyBackLight",
            new Vector3(0.4545195f, -0.7660444f, 0.4545195f),
            new Color(0.36f, 0.36f, 0.36f),
            new Color(0.36f, 0.36f, 0.36f),
            1.0f);
    }

    private static void AddDirectionalLight(
        World world,
        string entityName,
        Vector3 direction,
        Color diffuseColor,
        Color specularColor,
        float intensity,
        bool castShadows = false)
    {
        var entity = new Entity { Name = entityName };
        entity.RootComponent = new LightComponent
        {
            Type = LightType.Directional,
            LocalOrientation = CreateOrientationFromForward(direction),
            Color = diffuseColor,
            SpecularColor = specularColor,
            Intensity = intensity,
            CastShadows = castShadows,
        };

        world.AddEntity(entity);
    }

    private static Quaternion CreateOrientationFromForward(Vector3 forward)
    {
        if (forward.LengthSquared() <= 0.0001f)
        {
            return Quaternion.Identity;
        }

        forward = Vector3.Normalize(forward);
        float dot = Math.Clamp(Vector3.Dot(Vector3.Forward, forward), -1.0f, 1.0f);

        if (dot >= 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot <= -0.9999f)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        }

        var axis = Vector3.Normalize(Vector3.Cross(Vector3.Forward, forward));
        float angle = MathF.Acos(dot);
        return Quaternion.CreateFromAxisAngle(axis, angle);
    }
}