using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the migration of RPGDemo's sword/Link/octopus collision volumes from the legacy
/// per-sprite `collisions` arrays to the `collision_keyframes` timeline that
/// <see cref="AnimatedSpriteComponent"/> reads since commit 089c2e4c, done by
/// Tools/migrate-sprite-collisions-to-keyframes.py. Loads the shipped `.anim2d` assets exactly
/// the way the runtime does, through <see cref="Animation2dData.Load(JObject)"/>.
/// </summary>
public class RpgDemoCollisionKeyframesTests
{
    private static readonly string RepositoryRoot = GetRepositoryRoot();
    private static readonly string TileSetsDirectory = Path.Combine(RepositoryRoot, "Projects", "RPGDemo", "TileSets");

    [Fact]
    public void EveryAnimation_HasOneCollisionKeyframePerSpriteKeyframe()
    {
        var animationFiles = GetAnimationFilesWithSpriteTrack();

        //63 of the 64 .anim2d in RPGDemo/TileSets carry a Sprite track; swordman_composed_demo
        //(Position/DrawOrder tracks only) is the one animation the migration does not touch.
        Assert.Equal(63, animationFiles.Count);

        var profiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;

        foreach (var path in animationFiles)
        {
            var animationJson = JObject.Parse(File.ReadAllText(path));
            var animation = new Animation2dData();
            animation.Load(animationJson);

            int expectedKeyframeCount = CountDistinctSpriteKeyframeTimes(animationJson);
            Assert.Equal(expectedKeyframeCount, animation.CollisionKeyframes.Count);

            foreach (var keyframe in animation.CollisionKeyframes)
            {
                foreach (var fixture in keyframe.Fixtures)
                {
                    Assert.NotNull(fixture.Shape);
                    Assert.True(fixture.Shape is Box or Sphere, $"{path}: unexpected shape {fixture.Shape?.GetType()}");
                    Assert.NotNull(fixture.ProfileName);
                    Assert.True(profiles.TryGetProfileId(fixture.ProfileName, out _),
                        $"{path}: unknown collision profile '{fixture.ProfileName}'");
                }
            }
        }
    }

    [Fact]
    public void SwordAttackAnimations_CarryAttackVolumes()
    {
        var animationFiles = GetAnimationFilesWithSpriteTrack();

        var batonAttackFiles = animationFiles
            .Where(path => FileNameStartsWith(path, "baton_attack"))
            .ToList();
        Assert.NotEmpty(batonAttackFiles);
        foreach (var path in batonAttackFiles)
        {
            var animation = LoadAnimation(path);
            Assert.True(HasFixtureWithProfile(animation, CollisionProfileNames.AttackVolume),
                $"{path}: expected at least one AttackVolume fixture");
        }

        //Link's animations are named swordman_* (the sprites themselves are player_*), excluding
        //the composed demo (no Sprite track, already excluded from animationFiles) and the dead
        //animations, whose player_84..player_105 sprites carry no volume at all.
        var swordmanFiles = animationFiles
            .Where(path => FileNameStartsWith(path, "swordman_")
                && !FileNameStartsWith(path, "swordman_dead_stand")
                && !FileNameStartsWith(path, "swordman_dead_walking"))
            .ToList();
        Assert.NotEmpty(swordmanFiles);
        foreach (var path in swordmanFiles)
        {
            var animation = LoadAnimation(path);
            Assert.True(HasFixtureWithProfile(animation, CollisionProfileNames.DamageableVolume),
                $"{path}: expected at least one DamageableVolume fixture");
        }

        var octopusFiles = animationFiles
            .Where(path => FileNameStartsWith(path, "octopus_"))
            .ToList();
        Assert.NotEmpty(octopusFiles);
        foreach (var path in octopusFiles)
        {
            var animation = LoadAnimation(path);
            Assert.True(HasFixtureWithProfile(animation, CollisionProfileNames.DamageableVolume),
                $"{path}: expected at least one DamageableVolume fixture");
        }

        //A dead swordman never gets hit again: its keyframes are legitimately all-empty, the
        //script must not invent a volume that never existed on player_84..player_105.
        var deadFiles = animationFiles
            .Where(path => FileNameStartsWith(path, "swordman_dead_stand")
                || FileNameStartsWith(path, "swordman_dead_walking"))
            .ToList();
        Assert.NotEmpty(deadFiles);
        foreach (var path in deadFiles)
        {
            var animation = LoadAnimation(path);
            foreach (var keyframe in animation.CollisionKeyframes)
            {
                Assert.Empty(keyframe.Fixtures);
            }
        }
    }

    [Fact]
    public void SwordKeyframe_PlacesTheVolumeLikeTheSpriteHelperDid()
    {
        //sword_29.sprite: hotspot (-15, 17), volume location (16, 8), w 34, h 7.
        //x = 16 - (-15) + 34/2 = 48 ; y = -(8 - 17 + 7/2) = 5.5
        ColliderFixture sword29Fixture = null;

        foreach (var path in GetAnimationFilesWithSpriteTrack())
        {
            var animation = LoadAnimation(path);
            foreach (var keyframe in animation.CollisionKeyframes)
            {
                sword29Fixture = keyframe.Fixtures.FirstOrDefault(f => f.Tag == "sword_29");
                if (sword29Fixture != null)
                {
                    break;
                }
            }

            if (sword29Fixture != null)
            {
                break;
            }
        }

        Assert.NotNull(sword29Fixture);
        var box = Assert.IsType<Box>(sword29Fixture.Shape);
        Assert.Equal(new Vector3(34f, 7f, 1f), box.Size);
        Assert.Equal(new Vector3(48f, 5.5f, 0f), sword29Fixture.LocalPosition);
    }

    /// <summary>
    /// End-to-end check that an activated sword keyframe reaches the physics world as an
    /// AttackVolume body, reusing the in-memory component setup of
    /// AnimatedSpriteCollisionTimelineTests, fed here with a real migrated .anim2d asset instead
    /// of a hand-built Animation2dData.
    /// </summary>
    [Fact]
    public void ActivatedSwordKeyframe_CreatesAttackBodies()
    {
        var animationPath = Path.Combine(TileSetsDirectory, "baton_attack2_right.anim2d");
        var animation = LoadAnimation(animationPath);

        //The very first collision keyframe (t=0.0) of baton_attack2_right already carries
        //AttackVolume fixtures (sword_28), so a single Update reaches it.
        Assert.NotEmpty(animation.CollisionKeyframes);
        Assert.NotEmpty(animation.CollisionKeyframes[0].Fixtures);

        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var world = new CasaEngine.Framework.Scene.World.World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.PhysicsWorld), physicsWorldContext);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var entityRoot = new TestSceneComponent();
        entity.RootComponent = entityRoot;

        var component = new AnimatedSpriteComponent { CreatePhysicsForEachFrame = true };
        entityRoot.AddChildComponent(component);

        SetPrivateField(component, "_physicsWorldContext", physicsWorldContext);
        component.AddAnimation(new Animation2d(animation));
        component.SetCurrentAnimation(0, true);

        component.Update(1f / 60f);

        var activeBodies = GetPrivateField<List<PhysicsBody>>(component, "_activeCollisionBodies");
        Assert.NotNull(activeBodies);
        Assert.NotEmpty(activeBodies);

        var profiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;
        var attackVolumeGroupBit = profiles.GetResolved(CollisionProfileIds.AttackVolume).GroupBit;
        Assert.Contains(activeBodies, body => body.CollisionProfile.GroupBit == attackVolumeGroupBit);

        component.Detach();
    }

    private static bool HasFixtureWithProfile(Animation2dData animation, string profileName)
    {
        foreach (var keyframe in animation.CollisionKeyframes)
        {
            foreach (var fixture in keyframe.Fixtures)
            {
                if (fixture.ProfileName == profileName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool FileNameStartsWith(string path, string prefix)
    {
        return Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.Ordinal);
    }

    private static Animation2dData LoadAnimation(string path)
    {
        var animation = new Animation2dData();
        animation.Load(JObject.Parse(File.ReadAllText(path)));
        return animation;
    }

    private static List<string> GetAnimationFilesWithSpriteTrack()
    {
        var result = new List<string>();

        foreach (var path in Directory.EnumerateFiles(TileSetsDirectory, "*.anim2d").OrderBy(p => p, StringComparer.Ordinal))
        {
            var animationJson = JObject.Parse(File.ReadAllText(path));
            if (HasSpriteTrack(animationJson))
            {
                result.Add(path);
            }
        }

        return result;
    }

    private static bool HasSpriteTrack(JObject animationJson)
    {
        if (animationJson["tracks"] is not JArray tracksArray)
        {
            return false;
        }

        foreach (var trackToken in tracksArray)
        {
            if (trackToken is JObject track && track["property"]?.Value<string>() == "Sprite")
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDistinctSpriteKeyframeTimes(JObject animationJson)
    {
        var times = new HashSet<float>();

        if (animationJson["tracks"] is JArray tracksArray)
        {
            foreach (var trackToken in tracksArray)
            {
                if (trackToken is not JObject track || track["property"]?.Value<string>() != "Sprite")
                {
                    continue;
                }

                if (track["sprite_keyframes"] is not JArray keyframesArray)
                {
                    continue;
                }

                foreach (var keyframeToken in keyframesArray)
                {
                    if (keyframeToken is JObject keyframe)
                    {
                        times.Add(keyframe["time_seconds"].Value<float>());
                    }
                }
            }
        }

        return times.Count;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(target);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other) : base(other)
        {
        }

        public override TestSceneComponent Clone() => new(this);
    }
}
