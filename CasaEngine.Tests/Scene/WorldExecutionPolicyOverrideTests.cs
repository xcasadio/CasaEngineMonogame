using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// A world can run under its own execution policy instead of the one of its game. Without one, it follows the game
/// as before. The own policy is set before the world has a game; only the named policies are used here.
/// </summary>
public class WorldExecutionPolicyOverrideTests
{
    private static readonly Guid SpriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000f1");

    private static CasaEngineGame CreateGame(GameplayExecutionPolicy policy)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(game, new GameComponentCollection());
        game.ExecutionPolicy = policy;
        return game;
    }

    /// <summary>The own policy is set before the game, as it is before <c>LoadContent</c>.</summary>
    private static World CreateWorld(GameplayExecutionPolicy gamePolicy, GameplayExecutionPolicy? ownPolicy)
    {
        var world = new World { Name = "OverrideWorld" };
        if (ownPolicy != null)
        {
            world.ExecutionPolicyOverride = ownPolicy;
        }

        SetProperty(world, nameof(World.Game), CreateGame(gamePolicy));
        return world;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private static AnimatedSpriteComponent CreateAnimatedSprite(World world, AnimationType animationType = AnimationType.Loop)
    {
        var data = new Animation2dData { Name = "loop", AnimationType = animationType };
        data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = SpriteId });
        var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, SpriteId));
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(1f, SpriteId));
        data.Tracks.Add(track);

        var component = new AnimatedSpriteComponent();
        component.AddAnimation(new Animation2d(data));
        component.SetCurrentAnimation(0, true);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);
        entity.RootComponent = component;
        return component;
    }

    private static ParticleSystemComponent CreateParticleSystem(World world)
    {
        var component = new ParticleSystemComponent { SimulateInEditor = false };
        var entity = new Entity();
        entity.RootComponent = component;
        entity.InitializeWithWorld(world);
        component.SetParticleEffectAsset(CreateRuntimeAsset());
        return component;
    }

    private static ParticleEffectAsset CreateRuntimeAsset()
    {
        var asset = new ParticleEffectAsset();
        var emitter = new ParticleEmitterDefinition { Duration = 10.0f, MaxParticles = 8 };
        emitter.Emission.RateOverTime = 1.0f;
        emitter.Initial.Lifetime = FloatRange.Constant(10.0f);
        emitter.Initial.Speed = FloatRange.Constant(0.0f);
        emitter.Initial.Size = Vector2Range.Constant(Vector2.One);
        emitter.Initial.Rotation = FloatRange.Constant(0.0f);
        emitter.Initial.AngularVelocity = FloatRange.Constant(0.0f);
        emitter.Initial.StartColor = ColorGradient.Constant(Color.White);
        emitter.Simulation.Gravity = Vector3.Zero;
        emitter.Simulation.GravityScale = 0.0f;
        emitter.Simulation.Drag = 0.0f;
        emitter.Simulation.SizeOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.AlphaOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.VelocityOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.ColorOverLifetime = ColorGradient.Constant(Color.White);
        asset.Emitters.Add(emitter);
        return asset;
    }

    private sealed class CountingProxy : GameplayProxy
    {
        public int Updates;
        public int BeginPlays;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime) => Updates++;

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision)
        {
        }

        public override void OnHitEnded(Collision collision)
        {
        }

        public override void OnBeginPlay(World world) => BeginPlays++;

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    [Fact]
    public void AWorldWithoutAnOwnPolicy_FollowsThePolicyOfItsGame()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, ownPolicy: null);

        Assert.Null(world.ExecutionPolicyOverride);
        Assert.Same(GameplayExecutionPolicies.EditorPreview, world.ExecutionPolicy);
    }

    [Fact]
    public void AWorldWithAnOwnPolicy_RunsUnderItInsteadOfThePolicyOfItsGame()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, GameplayExecutionPolicies.EditorSimulation);

        Assert.Same(GameplayExecutionPolicies.EditorSimulation, world.ExecutionPolicy);
        Assert.Same(GameplayExecutionPolicies.EditorPreview, world.Game.ExecutionPolicy);
    }

    [Fact]
    public void AWorldWithoutAGame_HasANullPolicyAndDoesNotThrow()
    {
        var world = new World();

        GameplayExecutionPolicy? policy = null;
        var exception = Record.Exception(() => policy = world.ExecutionPolicy);

        Assert.Null(exception);
        Assert.Null(policy);
    }

    [Fact]
    public void AWorldWithoutAGame_RunsUnderItsOwnPolicy()
    {
        var world = new World { ExecutionPolicyOverride = GameplayExecutionPolicies.EditorSimulation };

        Assert.Same(GameplayExecutionPolicies.EditorSimulation, world.ExecutionPolicy);
    }

    [Fact]
    public void SettingTheOwnPolicy_OnceTheWorldHasAGame_Throws()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, ownPolicy: null);

        Assert.Throws<InvalidOperationException>(() => world.ExecutionPolicyOverride = GameplayExecutionPolicies.EditorSimulation);
        Assert.Null(world.ExecutionPolicyOverride);
    }

    [Fact]
    public void AnAnimation_DoesNotAdvanceWithoutAnOwnPolicy_UnderAnEditorPreviewGame()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, ownPolicy: null);
        var sprite = CreateAnimatedSprite(world);
        float before = sprite.CurrentAnimationTimeSeconds;

        sprite.Update(0.25f);

        Assert.Equal(before, sprite.CurrentAnimationTimeSeconds);
    }

    [Fact]
    public void AnAnimation_AdvancesByTheGivenStep_WithTheOwnPolicyEditorSimulation()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, GameplayExecutionPolicies.EditorSimulation);
        var sprite = CreateAnimatedSprite(world);
        float before = sprite.CurrentAnimationTimeSeconds;

        sprite.Update(0.25f);

        Assert.Equal(before + 0.25f, sprite.CurrentAnimationTimeSeconds, 4);
    }

    [Fact]
    public void Particles_DoNotSimulateWithoutAnOwnPolicy_WhenSimulateInEditorIsOff()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, ownPolicy: null);
        var particles = CreateParticleSystem(world);

        particles.Update(1.0f);

        Assert.Equal(0, particles.LastEmittedCount);
        Assert.Equal(0, particles.RuntimeInstance!.AliveCount);
    }

    [Fact]
    public void Particles_Simulate_WithTheOwnPolicyEditorSimulation()
    {
        var world = CreateWorld(GameplayExecutionPolicies.EditorPreview, GameplayExecutionPolicies.EditorSimulation);
        var particles = CreateParticleSystem(world);

        particles.Update(1.0f);

        Assert.Equal(1, particles.LastEmittedCount);
        Assert.Equal(1, particles.RuntimeInstance!.AliveCount);
    }

    [Fact]
    public void UnderARuntimeGame_AWorldWithTheOwnPolicyEditorPreview_RunsNeitherBeginPlayNorTheScriptUpdate()
    {
        var world = CreateWorld(GameplayExecutionPolicies.Runtime, GameplayExecutionPolicies.EditorPreview);
        var worldProxy = new CountingProxy();
        SetProperty(world, nameof(World.GameplayProxy), worldProxy);
        var entityProxy = new CountingProxy();
        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);
        SetProperty(entity, nameof(Entity.GameplayProxy), entityProxy);

        world.BeginPlay();
        entity.Update(0.016f);

        Assert.Equal(0, worldProxy.BeginPlays);
        Assert.Equal(0, entityProxy.Updates);
    }

    [Fact]
    public void UnderARuntimeGame_AWorldWithoutAnOwnPolicy_RunsBeginPlayAndTheScriptUpdate()
    {
        var world = CreateWorld(GameplayExecutionPolicies.Runtime, ownPolicy: null);
        var worldProxy = new CountingProxy();
        SetProperty(world, nameof(World.GameplayProxy), worldProxy);
        var entityProxy = new CountingProxy();
        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);
        SetProperty(entity, nameof(Entity.GameplayProxy), entityProxy);

        world.BeginPlay();
        entity.Update(0.016f);

        Assert.Equal(1, worldProxy.BeginPlays);
        Assert.Equal(1, entityProxy.Updates);
    }

    [Fact]
    public void AnAnimationOfALivePreviewWorld_AdvancesButRaisesNoAnimationEvent()
    {
        var world = CreateWorld(GameplayExecutionPolicies.Runtime, GameplayExecutionPolicies.EditorLivePreview);
        var once = CreateAnimatedSprite(world, AnimationType.Once);
        var loop = CreateAnimatedSprite(world, AnimationType.Loop);
        int finished = 0;
        int looped = 0;
        once.AnimationFinished += (_, _) => finished++;
        loop.AnimationLooped += (_, _) => looped++;

        once.Update(2.5f);
        loop.Update(2.5f);

        Assert.True(once.CurrentAnimationTimeSeconds > 0f);
        Assert.Equal(0, finished);
        Assert.Equal(0, looped);
    }

    [Fact]
    public void AnAnimationOfARuntimeWorld_RaisesItsAnimationEvents()
    {
        var world = CreateWorld(GameplayExecutionPolicies.Runtime, ownPolicy: null);
        var once = CreateAnimatedSprite(world, AnimationType.Once);
        var loop = CreateAnimatedSprite(world, AnimationType.Loop);
        int finished = 0;
        int looped = 0;
        once.AnimationFinished += (_, _) => finished++;
        loop.AnimationLooped += (_, _) => looped++;

        once.Update(2.5f);
        loop.Update(2.5f);

        Assert.Equal(1, finished);
        Assert.True(looped > 0);
    }
}
