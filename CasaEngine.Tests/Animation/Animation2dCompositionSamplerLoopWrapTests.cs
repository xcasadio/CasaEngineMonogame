using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// A loop whose float32 time lands exactly on its duration used to stay above the duration for ever and
/// to sample the hidden end key (O-E19-10 of the Alundra port). It wraps now, as the modulo rule of
/// <c>docs/engine/animation2d-composed-format-v1.md</c> says. The expected values come from a float32
/// model of the sampler, written before the fix.
/// </summary>
public class Animation2dCompositionSamplerLoopWrapTests
{
    private const string PartId = "sprite";

    private static readonly Guid SpriteA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SpriteB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid SpriteC = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid SpriteD = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");

    /// <summary>Shape of animation 53 of the hero: four visible sprite keys, hidden key at the duration.</summary>
    private static Animation2dData CreateHeroLikeLoop(AnimationType type = AnimationType.Loop)
    {
        var animation = new Animation2dData { Name = "hero_53", AnimationType = type };
        animation.Parts.Add(new Animation2dPartData { Id = PartId, DefaultVisible = false });

        var spriteTrack = new Animation2dTrackData { TargetPartId = PartId, Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, SpriteA));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.08f, SpriteB));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.16f, SpriteC));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.24f, SpriteD));
        animation.Tracks.Add(spriteTrack);

        var visibleTrack = new Animation2dTrackData { TargetPartId = PartId, Property = Animation2dTrackProperty.Visible };
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0f, true));
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.08f, true));
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.16f, true));
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.24f, true));
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.32f, false));
        animation.Tracks.Add(visibleTrack);
        return animation;
    }

    /// <summary>One visible sprite key at 0 and a hidden key at the duration.</summary>
    private static Animation2dData CreateLoopWithHiddenEnd(float durationSeconds)
    {
        var animation = new Animation2dData { Name = "loop", AnimationType = AnimationType.Loop };
        animation.Parts.Add(new Animation2dPartData { Id = PartId, DefaultVisible = false });

        var spriteTrack = new Animation2dTrackData { TargetPartId = PartId, Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, SpriteA));
        animation.Tracks.Add(spriteTrack);

        var visibleTrack = new Animation2dTrackData { TargetPartId = PartId, Property = Animation2dTrackProperty.Visible };
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0f, true));
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(durationSeconds, false));
        animation.Tracks.Add(visibleTrack);
        return animation;
    }

    private static Animation2dCompositionSampler CreateSampler(Animation2dData animation)
    {
        return new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));
    }

    private static Guid ExpectedSprite(int positionInCycle)
    {
        // Position 1..16 inside a 16 update cycle: B from the 4th, C from the 9th, D from the 13th, A again at the wrap.
        if (positionInCycle < 4) return SpriteA;
        if (positionInCycle < 9) return SpriteB;
        if (positionInCycle < 13) return SpriteC;
        if (positionInCycle < 16) return SpriteD;
        return SpriteA;
    }

    [Fact]
    public void R1_HeroLikeLoop_WrapsOnTheSixteenthUpdate_AndNeverSamplesTheHiddenEndKey()
    {
        var sampler = CreateSampler(CreateHeroLikeLoop());

        for (int i = 0; i < 15; i++)
        {
            sampler.Update(0.02f);
        }

        Assert.Equal(0.29999998f, sampler.CurrentTime);
        Assert.True(sampler.RuntimeState.Parts[0].Visible);
        Assert.Equal(SpriteD, sampler.RuntimeState.Parts[0].SpriteId);

        sampler.Update(0.02f);
        Assert.Equal(0f, sampler.CurrentTime);
        Assert.True(sampler.RuntimeState.Parts[0].Visible);
        Assert.Equal(SpriteA, sampler.RuntimeState.Parts[0].SpriteId);

        sampler.Update(0.02f);
        Assert.Equal(0.02f, sampler.CurrentTime);

        sampler = CreateSampler(CreateHeroLikeLoop());
        for (int update = 1; update <= 160; update++)
        {
            sampler.Update(0.02f);

            Assert.True(sampler.RuntimeState.Parts[0].Visible, $"hidden at update {update}");
            Assert.True(sampler.CurrentTime < 0.32f, $"time {sampler.CurrentTime} at update {update}");
            if (update % 16 == 0)
            {
                Assert.Equal(0f, sampler.CurrentTime);
            }

            Assert.Equal(ExpectedSprite(((update - 1) % 16) + 1), sampler.RuntimeState.Parts[0].SpriteId);
        }
    }

    [Theory]
    [InlineData(0.27999997f, 14)]
    [InlineData(0.29999998f, 15)]
    [InlineData(0.32f, 16)]
    [InlineData(0.36f, 18)]
    [InlineData(0.38000003f, 19)]
    public void R2_LoopWrapsOnTheTickWhoseDurationItLandsOn(float durationSeconds, int ticks)
    {
        var sampler = CreateSampler(CreateLoopWithHiddenEnd(durationSeconds));

        for (int update = 1; update <= 10 * ticks; update++)
        {
            sampler.Update(0.02f);

            Assert.True(sampler.CurrentTime < durationSeconds, $"time {sampler.CurrentTime} at update {update}");
            if (update == ticks - 1)
            {
                Assert.True(sampler.CurrentTime > 0f);
            }

            if (update == ticks)
            {
                Assert.Equal(0f, sampler.CurrentTime);
            }
        }
    }

    [Fact]
    public void R3_SeekToTheDuration_ThenUpdate_WrapsInsteadOfStayingAboveTheDuration()
    {
        var sampler = CreateSampler(CreateHeroLikeLoop());

        sampler.Seek(0.32f);
        Assert.Equal(0.32f, sampler.CurrentTime);
        Assert.False(sampler.RuntimeState.Parts[0].Visible);

        sampler.Update(0.02f);

        Assert.Equal(0.02f, sampler.CurrentTime);
        Assert.True(sampler.RuntimeState.Parts[0].Visible);
        Assert.Equal(SpriteA, sampler.RuntimeState.Parts[0].SpriteId);
    }

    [Fact]
    public void R4_Component_RaisesOneAnimationLoopedPerTurn_OnTheRealTimePath()
    {
        var component = AnimatedSpriteClockTestHost.Create(out _, out _);
        component.AddAnimation(new Animation2d(CreateHeroLikeLoop()));
        component.SetCurrentAnimation(0, true);

        var turnsAtUpdate = new List<int>();
        int update = 0;
        component.AnimationLooped += (_, _) => turnsAtUpdate.Add(update);

        for (update = 1; update <= 48; update++)
        {
            component.Update(0.02f);
        }

        Assert.Equal(new[] { 16, 32, 48 }, turnsAtUpdate);

        component = AnimatedSpriteClockTestHost.Create(out _, out _);
        component.AddAnimation(new Animation2d(CreateHeroLikeLoop()));
        component.SetCurrentAnimation(0, true);
        int count = 0;
        component.AnimationLooped += (_, _) => count++;

        component.Update(1f);

        Assert.Equal(3, count);
        Assert.Equal(0.04000002f, component.CurrentAnimationTimeSeconds);

        component = AnimatedSpriteClockTestHost.Create(out _, out _);
        component.AddAnimation(new Animation2d(CreateHeroLikeLoop()));
        component.SetCurrentAnimation(0, true);
        count = 0;
        component.AnimationLooped += (_, _) => count++;

        component.SeekCurrentAnimation(0.32f);
        component.Update(0.02f);

        Assert.Equal(1, count);
    }
}

/// <summary>Builds an <see cref="AnimatedSpriteComponent"/> inside a stand-in world (Runtime execution policy).</summary>
internal static class AnimatedSpriteClockTestHost
{
    public static AnimatedSpriteComponent Create(out CasaEngine.Framework.Scene.World.World world, out CasaEngineGame game)
    {
        world = new CasaEngine.Framework.Scene.World.World();

        game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);

        var component = new AnimatedSpriteComponent();
        PlaceInWorld(component, world);
        return component;
    }

    /// <summary>Puts a component under a fresh entity of the stand-in world, as a spawned entity would be.</summary>
    public static void PlaceInWorld(AnimatedSpriteComponent component, CasaEngine.Framework.Scene.World.World world)
    {
        var entityRoot = new HostSceneComponent();
        var entity = new Entity { RootComponent = entityRoot };
        SetProperty(entity, nameof(Entity.World), world);
        entityRoot.AddChildComponent(component);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class HostSceneComponent : SceneComponent
    {
        public HostSceneComponent()
        {
        }

        private HostSceneComponent(HostSceneComponent other) : base(other)
        {
        }

        public override HostSceneComponent Clone() => new(this);
    }
}
