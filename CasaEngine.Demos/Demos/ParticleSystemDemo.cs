using System;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

public sealed class ParticleSystemDemo : Demo
{
    private const float BurstIntervalSeconds = 1.35f;
    private static readonly Guid SoftParticleTextureAssetId = new("2af8c1e7-8d4e-4a87-b146-8b84d888fff2");
    private ParticleSystemComponent? _sparkBurst;
    private ParticleSystemComponent? _smokePuff;
    private float _burstTimer;

    public override string Title => "Particle system demo";

    public override string Description =>
        "CPU particle runtime with looping additive fire, recurring spark bursts, alpha smoke puffs, billboards, sorting and per-view render stats.";

    public override void Initialize(CasaEngineGame game)
    {
        Guid textureAssetId = ResolveDefaultTextureAssetId(game);
        var world = game.GameManager.CurrentWorld;

        AddGround(game);
        AddParticleEntity(world, "Fire Loop", new Vector3(0.0f, 0.1f, 0.0f), CreateFireLoop(), playOnStart: true, out _);
        AddParticleEntity(world, "Spark Burst", new Vector3(-2.4f, 0.35f, 0.0f), CreateSparkBurst(textureAssetId), playOnStart: true, out _sparkBurst);
        AddParticleEntity(world, "Smoke Puff", new Vector3(2.4f, 0.35f, 0.0f), CreateSmokePuff(textureAssetId), playOnStart: true, out _smokePuff);
        _burstTimer = BurstIntervalSeconds;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(0.0f, 4.2f, 8.5f), new Vector3(0.0f, 0.8f, 0.0f), Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        _burstTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_burstTimer < BurstIntervalSeconds)
        {
            return;
        }

        _burstTimer = 0.0f;
        _sparkBurst?.Restart(clearParticles: true);
        _smokePuff?.Restart(clearParticles: true);
    }

    public override void Clean()
    {
        _sparkBurst = null;
        _smokePuff = null;
        _burstTimer = 0.0f;
    }

    private static void AddGround(CasaEngineGame game)
    {
        var entity = new Entity { Name = "particle demo ground" };
        var meshComponent = new StaticModelComponent();
        entity.RootComponent = meshComponent;
        meshComponent.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(8.0f, 0.08f, 4.5f));
        meshComponent.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        meshComponent.StaticModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            DiffuseColor = new Color(42, 48, 58),
            AmbientColor = new Vector3(0.2f, 0.22f, 0.25f),
            SpecularColor = Vector3.Zero,
        };
        meshComponent.LocalPosition = new Vector3(0.0f, -0.08f, 0.0f);
        game.GameManager.CurrentWorld.AddEntity(entity);
    }

    private static void AddParticleEntity(
        CasaEngine.Framework.Scene.World.World world,
        string name,
        Vector3 position,
        ParticleEffectAsset asset,
        bool playOnStart,
        out ParticleSystemComponent component)
    {
        var entity = new Entity { Name = name };
        component = new ParticleSystemComponent
        {
            PlayOnStart = playOnStart,
            Looping = asset.Emitters[0].Looping,
            SimulateInEditor = true,
        };
        entity.RootComponent = component;
        component.LocalPosition = position;
        component.SetParticleEffectAsset(asset);
        world.AddEntity(entity);
    }

    private static ParticleEffectAsset CreateFireLoop()
    {
        var asset = new ParticleEffectAsset { Name = "FireLoop_Minimal" };
        var emitter = new ParticleEmitterDefinition
        {
            Name = "Fire Loop",
            Duration = 1.2f,
            Looping = true,
            MaxParticles = 128,
        };

        emitter.Emission.RateOverTime = 23.0f;
        emitter.Shape.ShapeType = ParticleShapeType.Point;
        emitter.Shape.Radius = 0.28f;
        emitter.Shape.AngleDegrees = 18.0f;
        emitter.Initial.Lifetime = new FloatRange(0.55f, 0.95f);
        emitter.Initial.Speed = new FloatRange(0.8f, 1.75f);
        emitter.Initial.Rotation = new FloatRange(-0.4f, 0.4f);
        emitter.Initial.AngularVelocity = new FloatRange(-1.2f, 1.2f);
        emitter.Initial.Size = new Vector2Range(new Vector2(0.14f, 0.2f), new Vector2(0.28f, 0.42f));
        emitter.Initial.StartColor = CreateFireLoopStartColor();
        emitter.Simulation.SimulationSpace = ParticleSimulationSpace.World;
        emitter.Simulation.Gravity = new Vector3(0.0f, 1.6f, 0.0f);
        emitter.Simulation.GravityScale = 0.45f;
        emitter.Simulation.Drag = 0.2f;
        emitter.Simulation.SizeOverLifetime = CreateFireLoopSizeOverLifetime();
        emitter.Simulation.AlphaOverLifetime = CreateFireLoopAlphaOverLifetime();
        emitter.Simulation.VelocityOverLifetime = CreateFireLoopVelocityOverLifetime();
        emitter.Simulation.ColorOverLifetime = CreateFireLoopColorOverLifetime();
        emitter.Renderer.TextureAssetId = SoftParticleTextureAssetId;
        emitter.Renderer.Flipbook.FrameOverLifetime = CreateFireLoopFrameOverLifetime();
        emitter.Renderer.BlendMode = ParticleBlendMode.Additive;
        emitter.Renderer.SortMode = ParticleSortMode.Distance;
        emitter.Renderer.RenderQueue = 3050;
        asset.Emitters.Add(emitter);
        return asset;
    }

    private static ColorGradient CreateFireLoopStartColor()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, new Color(255, 215, 59));
        gradient.AddAlphaKey(0.0f, 0.9f);
        gradient.AddAlphaKey(1.0f, 0.0f);
        return gradient;
    }

    private static FloatCurve CreateFireLoopSizeOverLifetime()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.55f);
        curve.AddKey(0.35f, 1.15f);
        curve.AddKey(1.0f, 0.39999998f);
        return curve;
    }

    private static FloatCurve CreateFireLoopAlphaOverLifetime()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, -0.05f);
        curve.AddKey(0.12f, 1.0f);
        curve.AddKey(1.0f, -0.05f);
        return curve;
    }

    private static FloatCurve CreateFireLoopVelocityOverLifetime()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.95f);
        curve.AddKey(1.0f, 0.3f);
        return curve;
    }

    private static ColorGradient CreateFireLoopColorOverLifetime()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, Color.White);
        gradient.AddColorKey(0.38f, new Color(255, 128, 24));
        gradient.AddColorKey(0.72f, new Color(214, 32, 32));
        gradient.AddColorKey(1.0f, new Color(84, 75, 73));
        gradient.AddAlphaKey(0.0f, 1.0f);
        gradient.AddAlphaKey(1.0f, 0.0f);
        return gradient;
    }

    private static FloatCurve CreateFireLoopFrameOverLifetime()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 0.0f);
        curve.AddKey(1.0f, -0.05f);
        return curve;
    }

    private static ParticleEffectAsset CreateSparkBurst(Guid textureAssetId)
    {
        var asset = new ParticleEffectAsset { Name = "Demo_SparkBurst" };
        var emitter = new ParticleEmitterDefinition
        {
            Name = "Spark Burst",
            Duration = 0.8f,
            Looping = false,
            MaxParticles = 96,
        };

        emitter.Emission.Bursts.Add(new ParticleBurst { Time = 0.0f, CountMin = 28, CountMax = 40 });
        emitter.Shape.ShapeType = ParticleShapeType.Cone;
        emitter.Shape.AngleDegrees = 38.0f;
        emitter.Shape.EmitFromShell = true;
        emitter.Initial.Lifetime = new FloatRange(0.25f, 0.65f);
        emitter.Initial.Speed = new FloatRange(2.0f, 4.5f);
        emitter.Initial.Rotation = new FloatRange(-MathHelper.Pi, MathHelper.Pi);
        emitter.Initial.AngularVelocity = new FloatRange(-2.0f, 2.0f);
        emitter.Initial.Size = new Vector2Range(new Vector2(0.04f), new Vector2(0.09f));
        emitter.Initial.StartColor = ColorGradient.Fire();
        emitter.Simulation.SimulationSpace = ParticleSimulationSpace.World;
        emitter.Simulation.Gravity = new Vector3(0.0f, -9.8f, 0.0f);
        emitter.Simulation.GravityScale = 0.18f;
        emitter.Simulation.Drag = 0.08f;
        emitter.Simulation.SizeOverLifetime = FloatCurve.FadeOut();
        emitter.Simulation.AlphaOverLifetime = FloatCurve.FadeOut();
        emitter.Simulation.VelocityOverLifetime = FloatCurve.Constant(0.7f);
        emitter.Renderer.TextureAssetId = textureAssetId;
        emitter.Renderer.BlendMode = ParticleBlendMode.Additive;
        emitter.Renderer.SortMode = ParticleSortMode.Distance;
        emitter.Renderer.RenderQueue = 3100;
        emitter.Renderer.Layer = 1;
        asset.Emitters.Add(emitter);
        return asset;
    }

    private static ParticleEffectAsset CreateSmokePuff(Guid textureAssetId)
    {
        var asset = new ParticleEffectAsset { Name = "Demo_SmokePuff" };
        var emitter = new ParticleEmitterDefinition
        {
            Name = "Smoke Puff",
            Duration = 1.25f,
            Looping = false,
            MaxParticles = 64,
        };

        emitter.Emission.Bursts.Add(new ParticleBurst { Time = 0.0f, CountMin = 12, CountMax = 18 });
        emitter.Shape.ShapeType = ParticleShapeType.Cone;
        emitter.Shape.AngleDegrees = 22.0f;
        emitter.Initial.Lifetime = new FloatRange(0.75f, 1.35f);
        emitter.Initial.Speed = new FloatRange(0.35f, 0.9f);
        emitter.Initial.Rotation = new FloatRange(-0.4f, 0.4f);
        emitter.Initial.AngularVelocity = new FloatRange(-0.45f, 0.45f);
        emitter.Initial.Size = new Vector2Range(new Vector2(0.18f), new Vector2(0.42f));
        emitter.Initial.StartColor = ColorGradient.Smoke();
        emitter.Simulation.SimulationSpace = ParticleSimulationSpace.World;
        emitter.Simulation.Gravity = new Vector3(0.0f, 0.55f, 0.0f);
        emitter.Simulation.GravityScale = 0.3f;
        emitter.Simulation.Drag = 0.25f;
        emitter.Simulation.SizeOverLifetime = FloatCurve.Pulse();
        emitter.Simulation.AlphaOverLifetime = FloatCurve.FadeOut();
        emitter.Simulation.VelocityOverLifetime = FloatCurve.Constant(0.45f);
        emitter.Renderer.TextureAssetId = textureAssetId;
        emitter.Renderer.BlendMode = ParticleBlendMode.Alpha;
        emitter.Renderer.SortMode = ParticleSortMode.Distance;
        emitter.Renderer.RenderQueue = 3000;
        asset.Emitters.Add(emitter);
        return asset;
    }

    private static Guid ResolveDefaultTextureAssetId(CasaEngineGame game)
    {
        Texture? defaultTexture = game.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
        return defaultTexture?.Id ?? Guid.Empty;
    }
}