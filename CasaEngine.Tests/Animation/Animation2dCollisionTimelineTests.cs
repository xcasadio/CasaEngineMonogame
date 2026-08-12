using CasaEngine.EditorServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class Animation2dCollisionTimelineTests
{
    [Fact]
    public void CollisionKeyframes_RoundTripThroughTheEditorSerializer()
    {
        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0.25f,
            new ColliderFixture(new Box { Size = new Vector3(2f, 3f, 1f) })
            {
                LocalPosition = new Vector3(1f, 0f, 0f),
                LocalRotation = Quaternion.CreateFromYawPitchRoll(0.25f, 0f, 0f),
                ProfileName = CollisionProfileNames.AttackVolume,
                Tag = "sword",
            }));
        animation.CollisionKeyframes.Add(BuildKeyframe(0.75f,
            new ColliderFixture(new Sphere { Radius = 0.5f }) { Tag = "hurt_head" },
            new ColliderFixture(new Box()) { ProfileName = CollisionProfileNames.DamageableVolume }));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));

        var loaded = new Animation2dData();
        loaded.Load(document);

        Assert.Equal(2, loaded.CollisionKeyframes.Count);

        var first = loaded.CollisionKeyframes[0];
        Assert.Equal(0.25f, first.TimeSeconds);
        var firstFixture = Assert.Single(first.Fixtures);
        Assert.Equal(new Vector3(2f, 3f, 1f), Assert.IsType<Box>(firstFixture.Shape).Size);
        Assert.Equal(new Vector3(1f, 0f, 0f), firstFixture.LocalPosition);
        Assert.Equal(Quaternion.CreateFromYawPitchRoll(0.25f, 0f, 0f), firstFixture.LocalRotation);
        Assert.Equal(CollisionProfileNames.AttackVolume, firstFixture.ProfileName);
        Assert.Equal("sword", firstFixture.Tag);

        var second = loaded.CollisionKeyframes[1];
        Assert.Equal(0.75f, second.TimeSeconds);
        Assert.Equal(2, second.Fixtures.Count);
        Assert.Equal(0.5f, Assert.IsType<Sphere>(second.Fixtures[0].Shape).Radius);
        Assert.Equal("hurt_head", second.Fixtures[0].Tag);
        Assert.Equal(CollisionProfileNames.DamageableVolume, second.Fixtures[1].ProfileName);
    }

    [Fact]
    public void CollisionKeyframes_AreSortedByTimeOnLoad()
    {
        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0.8f, new ColliderFixture(new Box()) { Tag = "late" }));
        animation.CollisionKeyframes.Add(BuildKeyframe(0.1f, new ColliderFixture(new Box()) { Tag = "early" }));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));

        var loaded = new Animation2dData();
        loaded.Load(document);

        Assert.Equal("early", loaded.CollisionKeyframes[0].Fixtures[0].Tag);
        Assert.Equal("late", loaded.CollisionKeyframes[1].Fixtures[0].Tag);
    }

    [Fact]
    public void GetDurationSeconds_IncludesTheLastCollisionKeyframe()
    {
        var animation = new Animation2dData();
        animation.Events.Add(new AnimationEventAsset(0.2f, "Hit"));

        Assert.Equal(0.2f, animation.GetDurationSeconds());

        animation.CollisionKeyframes.Add(BuildKeyframe(0.9f, new ColliderFixture(new Box())));

        Assert.Equal(0.9f, animation.GetDurationSeconds());
    }

    [Fact]
    public void Sampling_UsesStepSemanticsAndReportsNoSetBeforeTheFirstKeyframe()
    {
        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0.2f, new ColliderFixture(new Box())));
        animation.CollisionKeyframes.Add(BuildKeyframe(0.5f, new ColliderFixture(new Box())));

        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        Assert.Equal(-1, sampler.CurrentCollisionKeyframeIndex);

        sampler.Seek(0.1f);
        Assert.Equal(-1, sampler.CurrentCollisionKeyframeIndex);

        sampler.Seek(0.2f);
        Assert.Equal(0, sampler.CurrentCollisionKeyframeIndex);

        sampler.Seek(0.4f);
        Assert.Equal(0, sampler.CurrentCollisionKeyframeIndex);

        sampler.Seek(0.5f);
        Assert.Equal(1, sampler.CurrentCollisionKeyframeIndex);

        //Past the duration a non looping animation clamps to its last keyframe.
        sampler.Seek(5f);
        Assert.Equal(1, sampler.CurrentCollisionKeyframeIndex);
    }

    [Fact]
    public void Sampling_WrapsBackToAnEarlyKeyframeWhenTheAnimationLoops()
    {
        var animation = new Animation2dData { AnimationType = AnimationType.Loop };
        animation.CollisionKeyframes.Add(BuildKeyframe(0f, new ColliderFixture(new Box())));
        animation.CollisionKeyframes.Add(BuildKeyframe(0.3f, new ColliderFixture(new Box())));
        animation.CollisionKeyframes.Add(BuildKeyframe(0.6f, new ColliderFixture(new Box())));
        //The event only stretches the duration past the last collision keyframe.
        animation.Events.Add(new AnimationEventAsset(1f, "End"));

        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        Assert.Equal(0, sampler.CurrentCollisionKeyframeIndex);

        sampler.Update(0.4f);
        Assert.Equal(1, sampler.CurrentCollisionKeyframeIndex);

        sampler.Update(0.3f);
        Assert.Equal(2, sampler.CurrentCollisionKeyframeIndex);

        //The duration is 1s: this step wraps past the end and lands back on the first keyframe.
        sampler.Update(0.5f);
        Assert.Equal(0, sampler.CurrentCollisionKeyframeIndex);
    }

    private static Animation2dCollisionKeyframeData BuildKeyframe(float timeSeconds, params ColliderFixture[] fixtures)
    {
        var keyframe = new Animation2dCollisionKeyframeData { TimeSeconds = timeSeconds };
        keyframe.Fixtures.AddRange(fixtures);
        return keyframe;
    }
}
