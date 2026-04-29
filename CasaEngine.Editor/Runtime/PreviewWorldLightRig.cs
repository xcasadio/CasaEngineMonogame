using System;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime;

internal static class PreviewWorldLightRig
{
    public static void AddDefaultLights(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        AddDirectionalLight(
            world,
            "PreviewDirectionalLight",
            new Vector3(-0.55f, -0.75f, -0.35f),
            new Color(255, 244, 214),
            Color.White,
            1.0f);

        AddPointLight(
            world,
            "PreviewPointLight",
            new Vector3(2.25f, 2.0f, 2.0f),
            new Color(110, 146, 210),
            new Color(110, 146, 210),
            0.55f,
            8.0f);

        AddSpotLight(
            world,
            "PreviewSpotLight",
            new Vector3(-2.5f, 3.0f, 1.5f),
            Vector3.Zero,
            new Color(255, 214, 168),
            new Color(255, 214, 168),
            0.85f,
            9.0f,
            18.0f,
            32.0f);
    }

    private static void AddDirectionalLight(World world, string name, Vector3 direction, Color color, Color specularColor, float intensity)
    {
        var light = new LightComponent
        {
            Type = LightType.Directional,
            Color = color,
            SpecularColor = specularColor,
            Intensity = intensity,
            LocalOrientation = CreateOrientationFromForward(Vector3.Normalize(direction)),
        };

        var entity = new Entity
        {
            Name = name,
            RootComponent = light,
        };
        world.AddEntity(entity);
    }

    private static void AddPointLight(World world, string name, Vector3 position, Color color, Color specularColor, float intensity, float range)
    {
        var light = new LightComponent
        {
            Type = LightType.Point,
            Color = color,
            SpecularColor = specularColor,
            Intensity = intensity,
            Range = range,
            LocalPosition = position,
        };

        var entity = new Entity
        {
            Name = name,
            RootComponent = light,
        };
        world.AddEntity(entity);
    }

    private static void AddSpotLight(
        World world,
        string name,
        Vector3 position,
        Vector3 target,
        Color color,
        Color specularColor,
        float intensity,
        float range,
        float innerConeAngleDegrees,
        float outerConeAngleDegrees)
    {
        var forward = target - position;
        if (forward.LengthSquared() <= 0.0001f)
        {
            forward = Vector3.Forward;
        }

        var light = new LightComponent
        {
            Type = LightType.Spot,
            Color = color,
            SpecularColor = specularColor,
            Intensity = intensity,
            Range = range,
            InnerConeAngleDegrees = innerConeAngleDegrees,
            OuterConeAngleDegrees = outerConeAngleDegrees,
            LocalPosition = position,
            LocalOrientation = CreateOrientationFromForward(Vector3.Normalize(forward)),
        };

        var entity = new Entity
        {
            Name = name,
            RootComponent = light,
        };
        world.AddEntity(entity);
    }

    private static Quaternion CreateOrientationFromForward(Vector3 forward)
    {
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