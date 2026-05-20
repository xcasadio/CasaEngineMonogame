using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Particles.Serialization;

public static class ParticleEffectAssetJsonSerializer
{
    public static void Save(ParticleEffectAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = asset.Id.ToString();
        node["name"] = asset.Name;
        node["type"] = nameof(ParticleEffectAsset);
        node["version"] = asset.Version;
        node["schema_version"] = ParticleEffectAsset.CurrentVersion;

        var emittersNode = new JArray();
        for (int emitterIndex = 0; emitterIndex < asset.Emitters.Count; emitterIndex++)
        {
            var emitterNode = new JObject();
            SaveEmitter(asset.Emitters[emitterIndex], emitterNode);
            emittersNode.Add(emitterNode);
        }

        node["emitters"] = emittersNode;
    }

    public static void Load(ParticleEffectAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        int version = node["version"]?.GetInt32() ?? ParticleEffectAsset.CurrentVersion;
        if (!CanMigrate(version))
        {
            throw new InvalidOperationException($"Particle effect asset version {version} is newer than supported version {ParticleEffectAsset.CurrentVersion}.");
        }

        MigrateToCurrent(node);

        asset.Version = ParticleEffectAsset.CurrentVersion;
        asset.Emitters.Clear();

        if (node["emitters"] is not JArray emittersNode)
        {
            return;
        }

        for (int emitterIndex = 0; emitterIndex < emittersNode.Count; emitterIndex++)
        {
            if (emittersNode[emitterIndex] is not JObject emitterNode)
            {
                continue;
            }

            asset.Emitters.Add(LoadEmitter(emitterNode));
        }
    }

    public static bool CanMigrate(int version)
        => version > 0 && version <= ParticleEffectAsset.CurrentVersion;

    public static void MigrateToCurrent(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node["version"] = ParticleEffectAsset.CurrentVersion;
        node["schema_version"] = ParticleEffectAsset.CurrentVersion;
    }

    private static void SaveEmitter(ParticleEmitterDefinition emitter, JObject node)
    {
        node["name"] = emitter.Name;
        node["enabled"] = emitter.Enabled;
        node["duration"] = emitter.Duration;
        node["looping"] = emitter.Looping;
        node["start_delay"] = emitter.StartDelay;
        node["max_particles"] = emitter.MaxParticles;

        node["emission"] = SaveEmission(emitter.Emission);
        node["shape"] = SaveShape(emitter.Shape);
        node["initial"] = SaveInitial(emitter.Initial);
        node["simulation"] = SaveSimulation(emitter.Simulation);
        node["renderer"] = SaveRenderer(emitter.Renderer);
    }

    private static ParticleEmitterDefinition LoadEmitter(JObject node)
    {
        return new ParticleEmitterDefinition
        {
            Name = node["name"]?.GetString() ?? "Emitter",
            Enabled = node["enabled"]?.GetBoolean() ?? true,
            Duration = node["duration"]?.GetSingle() ?? 5.0f,
            Looping = node["looping"]?.GetBoolean() ?? true,
            StartDelay = node["start_delay"]?.GetSingle() ?? 0.0f,
            MaxParticles = node["max_particles"]?.GetInt32() ?? 1000,
            Emission = LoadEmission(node["emission"] as JObject),
            Shape = LoadShape(node["shape"] as JObject),
            Initial = LoadInitial(node["initial"] as JObject),
            Simulation = LoadSimulation(node["simulation"] as JObject),
            Renderer = LoadRenderer(node["renderer"] as JObject),
        };
    }

    private static JObject SaveEmission(ParticleEmissionModule emission)
    {
        var node = new JObject
        {
            ["rate_over_time"] = emission.RateOverTime,
        };

        var burstsNode = new JArray();
        for (int burstIndex = 0; burstIndex < emission.Bursts.Count; burstIndex++)
        {
            ParticleBurst burst = emission.Bursts[burstIndex];
            burstsNode.Add(new JObject
            {
                ["time"] = burst.Time,
                ["count_min"] = burst.CountMin,
                ["count_max"] = burst.CountMax,
            });
        }

        node["bursts"] = burstsNode;
        return node;
    }

    private static ParticleEmissionModule LoadEmission(JObject? node)
    {
        var emission = new ParticleEmissionModule();
        if (node == null)
        {
            return emission;
        }

        emission.RateOverTime = node["rate_over_time"]?.GetSingle() ?? emission.RateOverTime;
        emission.Bursts.Clear();
        if (node["bursts"] is not JArray burstsNode)
        {
            return emission;
        }

        for (int burstIndex = 0; burstIndex < burstsNode.Count; burstIndex++)
        {
            if (burstsNode[burstIndex] is not JObject burstNode)
            {
                continue;
            }

            emission.Bursts.Add(new ParticleBurst
            {
                Time = burstNode["time"]?.GetSingle() ?? 0.0f,
                CountMin = burstNode["count_min"]?.GetInt32() ?? 1,
                CountMax = burstNode["count_max"]?.GetInt32() ?? 1,
            });
        }

        return emission;
    }

    private static JObject SaveShape(ParticleShapeModule shape)
        => new()
        {
            ["shape_type"] = shape.ShapeType.ToString(),
            ["size"] = SaveVector3(shape.Size),
            ["radius"] = shape.Radius,
            ["angle_degrees"] = shape.AngleDegrees,
            ["emit_from_shell"] = shape.EmitFromShell,
        };

    private static ParticleShapeModule LoadShape(JObject? node)
    {
        var shape = new ParticleShapeModule();
        if (node == null)
        {
            return shape;
        }

        shape.ShapeType = node["shape_type"] is { } shapeTypeToken ? shapeTypeToken.GetEnum<ParticleShapeType>() : shape.ShapeType;
        shape.Size = node["size"] is { } sizeToken ? sizeToken.GetVector3() : shape.Size;
        shape.Radius = node["radius"]?.GetSingle() ?? shape.Radius;
        shape.AngleDegrees = node["angle_degrees"]?.GetSingle() ?? shape.AngleDegrees;
        shape.EmitFromShell = node["emit_from_shell"]?.GetBoolean() ?? shape.EmitFromShell;
        return shape;
    }

    private static JObject SaveInitial(ParticleInitialModule initial)
        => new()
        {
            ["lifetime"] = SaveFloatRange(initial.Lifetime),
            ["speed"] = SaveFloatRange(initial.Speed),
            ["rotation"] = SaveFloatRange(initial.Rotation),
            ["angular_velocity"] = SaveFloatRange(initial.AngularVelocity),
            ["size"] = SaveVector2Range(initial.Size),
            ["start_color"] = SaveColorGradient(initial.StartColor),
        };

    private static ParticleInitialModule LoadInitial(JObject? node)
    {
        var initial = new ParticleInitialModule();
        if (node == null)
        {
            return initial;
        }

        initial.Lifetime = LoadFloatRange(node["lifetime"] as JObject, initial.Lifetime);
        initial.Speed = LoadFloatRange(node["speed"] as JObject, initial.Speed);
        initial.Rotation = LoadFloatRange(node["rotation"] as JObject, initial.Rotation);
        initial.AngularVelocity = LoadFloatRange(node["angular_velocity"] as JObject, initial.AngularVelocity);
        initial.Size = LoadVector2Range(node["size"] as JObject, initial.Size);
        initial.StartColor = LoadColorGradient(node["start_color"] as JObject, initial.StartColor);
        return initial;
    }

    private static JObject SaveSimulation(ParticleSimulationModule simulation)
        => new()
        {
            ["simulation_space"] = simulation.SimulationSpace.ToString(),
            ["gravity"] = SaveVector3(simulation.Gravity),
            ["gravity_scale"] = simulation.GravityScale,
            ["drag"] = simulation.Drag,
            ["size_over_lifetime"] = SaveFloatCurve(simulation.SizeOverLifetime),
            ["alpha_over_lifetime"] = SaveFloatCurve(simulation.AlphaOverLifetime),
            ["velocity_over_lifetime"] = SaveFloatCurve(simulation.VelocityOverLifetime),
            ["color_over_lifetime"] = SaveColorGradient(simulation.ColorOverLifetime),
        };

    private static ParticleSimulationModule LoadSimulation(JObject? node)
    {
        var simulation = new ParticleSimulationModule();
        if (node == null)
        {
            return simulation;
        }

        simulation.SimulationSpace = node["simulation_space"] is { } spaceToken ? spaceToken.GetEnum<ParticleSimulationSpace>() : simulation.SimulationSpace;
        simulation.Gravity = node["gravity"] is { } gravityToken ? gravityToken.GetVector3() : simulation.Gravity;
        simulation.GravityScale = node["gravity_scale"]?.GetSingle() ?? simulation.GravityScale;
        simulation.Drag = node["drag"]?.GetSingle() ?? simulation.Drag;
        simulation.SizeOverLifetime = LoadFloatCurve(node["size_over_lifetime"] as JObject, simulation.SizeOverLifetime);
        simulation.AlphaOverLifetime = LoadFloatCurve(node["alpha_over_lifetime"] as JObject, simulation.AlphaOverLifetime);
        simulation.VelocityOverLifetime = LoadFloatCurve(node["velocity_over_lifetime"] as JObject, simulation.VelocityOverLifetime);
        simulation.ColorOverLifetime = LoadColorGradient(node["color_over_lifetime"] as JObject, simulation.ColorOverLifetime);
        return simulation;
    }

    private static JObject SaveRenderer(ParticleRendererModule renderer)
        => new()
        {
            ["render_mode"] = renderer.RenderMode.ToString(),
            ["texture_asset_id"] = renderer.TextureAssetId.ToString(),
            ["blend_mode"] = renderer.BlendMode.ToString(),
            ["sort_mode"] = renderer.SortMode.ToString(),
            ["depth_test"] = renderer.DepthTest,
            ["depth_write"] = renderer.DepthWrite,
            ["render_queue"] = renderer.RenderQueue,
            ["layer"] = renderer.Layer,
            ["always_visible"] = renderer.AlwaysVisible,
        };

    private static ParticleRendererModule LoadRenderer(JObject? node)
    {
        var renderer = new ParticleRendererModule();
        if (node == null)
        {
            return renderer;
        }

        renderer.RenderMode = node["render_mode"] is { } renderModeToken ? renderModeToken.GetEnum<ParticleRenderMode>() : renderer.RenderMode;
        renderer.TextureAssetId = node["texture_asset_id"]?.GetGuid() ?? renderer.TextureAssetId;
        renderer.BlendMode = node["blend_mode"] is { } blendModeToken ? blendModeToken.GetEnum<ParticleBlendMode>() : renderer.BlendMode;
        renderer.SortMode = node["sort_mode"] is { } sortModeToken ? sortModeToken.GetEnum<ParticleSortMode>() : renderer.SortMode;
        renderer.DepthTest = node["depth_test"]?.GetBoolean() ?? renderer.DepthTest;
        renderer.DepthWrite = node["depth_write"]?.GetBoolean() ?? renderer.DepthWrite;
        renderer.RenderQueue = node["render_queue"]?.GetInt32() ?? renderer.RenderQueue;
        renderer.Layer = node["layer"]?.GetInt32() ?? renderer.Layer;
        renderer.AlwaysVisible = node["always_visible"]?.GetBoolean() ?? renderer.AlwaysVisible;
        return renderer;
    }

    private static JObject SaveFloatRange(FloatRange range)
        => new()
        {
            ["min"] = range.Min,
            ["max"] = range.Max,
        };

    private static FloatRange LoadFloatRange(JObject? node, FloatRange fallback)
    {
        if (node == null)
        {
            return fallback;
        }

        return new FloatRange(node["min"]?.GetSingle() ?? fallback.Min, node["max"]?.GetSingle() ?? fallback.Max);
    }

    private static JObject SaveVector2Range(Vector2Range range)
        => new()
        {
            ["min"] = SaveVector2(range.Min),
            ["max"] = SaveVector2(range.Max),
        };

    private static Vector2Range LoadVector2Range(JObject? node, Vector2Range fallback)
    {
        if (node == null)
        {
            return fallback;
        }

        Vector2 min = node["min"] is { } minToken ? minToken.GetVector2() : fallback.Min;
        Vector2 max = node["max"] is { } maxToken ? maxToken.GetVector2() : fallback.Max;
        return new Vector2Range(min, max);
    }

    private static JObject SaveFloatCurve(FloatCurve curve)
    {
        var node = new JObject();
        var keysNode = new JArray();
        for (int keyIndex = 0; keyIndex < curve.Keys.Count; keyIndex++)
        {
            FloatCurveKey key = curve.Keys[keyIndex];
            keysNode.Add(new JObject
            {
                ["time"] = key.Time,
                ["value"] = key.Value,
            });
        }

        node["keys"] = keysNode;
        return node;
    }

    private static FloatCurve LoadFloatCurve(JObject? node, FloatCurve fallback)
    {
        if (node == null || node["keys"] is not JArray keysNode)
        {
            return fallback;
        }

        var curve = new FloatCurve();
        for (int keyIndex = 0; keyIndex < keysNode.Count; keyIndex++)
        {
            if (keysNode[keyIndex] is not JObject keyNode)
            {
                continue;
            }

            curve.AddKey(keyNode["time"]?.GetSingle() ?? 0.0f, keyNode["value"]?.GetSingle() ?? 0.0f);
        }

        return curve;
    }

    private static JObject SaveColorGradient(ColorGradient gradient)
    {
        var node = new JObject();
        var colorKeysNode = new JArray();
        for (int keyIndex = 0; keyIndex < gradient.ColorKeys.Count; keyIndex++)
        {
            ColorGradientKey key = gradient.ColorKeys[keyIndex];
            colorKeysNode.Add(new JObject
            {
                ["time"] = key.Time,
                ["color"] = SaveColor(key.Color),
            });
        }

        var alphaKeysNode = new JArray();
        for (int keyIndex = 0; keyIndex < gradient.AlphaKeys.Count; keyIndex++)
        {
            AlphaGradientKey key = gradient.AlphaKeys[keyIndex];
            alphaKeysNode.Add(new JObject
            {
                ["time"] = key.Time,
                ["alpha"] = key.Alpha,
            });
        }

        node["color_keys"] = colorKeysNode;
        node["alpha_keys"] = alphaKeysNode;
        return node;
    }

    private static ColorGradient LoadColorGradient(JObject? node, ColorGradient fallback)
    {
        if (node == null)
        {
            return fallback;
        }

        var gradient = new ColorGradient();
        if (node["color_keys"] is JArray colorKeysNode)
        {
            for (int keyIndex = 0; keyIndex < colorKeysNode.Count; keyIndex++)
            {
                if (colorKeysNode[keyIndex] is not JObject keyNode)
                {
                    continue;
                }

                Color color = keyNode["color"] is { } colorToken ? colorToken.GetColor() : Color.White;
                gradient.AddColorKey(keyNode["time"]?.GetSingle() ?? 0.0f, color);
            }
        }

        if (node["alpha_keys"] is JArray alphaKeysNode)
        {
            for (int keyIndex = 0; keyIndex < alphaKeysNode.Count; keyIndex++)
            {
                if (alphaKeysNode[keyIndex] is not JObject keyNode)
                {
                    continue;
                }

                gradient.AddAlphaKey(keyNode["time"]?.GetSingle() ?? 0.0f, keyNode["alpha"]?.GetSingle() ?? 1.0f);
            }
        }

        return gradient.ColorKeys.Count == 0 && gradient.AlphaKeys.Count == 0 ? fallback : gradient;
    }

    private static JObject SaveVector2(Vector2 value)
        => new()
        {
            ["x"] = value.X,
            ["y"] = value.Y,
        };

    private static JObject SaveVector3(Vector3 value)
        => new()
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };

    private static JObject SaveColor(Color color)
        => new()
        {
            ["r"] = color.R,
            ["g"] = color.G,
            ["b"] = color.B,
            ["a"] = color.A,
        };
}