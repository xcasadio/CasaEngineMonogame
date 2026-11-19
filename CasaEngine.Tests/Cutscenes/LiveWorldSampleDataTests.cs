using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// Guards the data of the live world of Projects/SpriteCutsceneSample: the world, its cutscene, its animation and its
/// particle effect load without a format error, the cutscene validates and names only fixed sprites, and every reference
/// of the world resolves in the sample's own catalogue.
/// </summary>
public class LiveWorldSampleDataTests
{
    private static readonly string[] LiveEntities = ["AnimatedOrb", "Sparks", "FallingBox", "Floor", "Marker"];

    [Fact]
    public void TheLiveCutscene_ValidatesWithNoErrorAndNoWarning()
    {
        var result = CutsceneValidator.Validate(LoadCutscene());

        Assert.Empty(result.Messages);
    }

    [Fact]
    public void TheLiveCutscene_LastsAtLeastTwoSecondsAndNamesTheMarkerAlone()
    {
        var actions = Flatten(LoadCutscene().RootAction).ToList();
        var names = new List<string>();
        var seconds = 0f;
        var from = System.Numerics.Vector2.Zero;

        foreach (var action in actions)
        {
            switch (action)
            {
                case ShowSpriteCutsceneActionData show:
                    names.Add(show.EntityName!);
                    from = new System.Numerics.Vector2(show.X!.Value, show.Y!.Value);
                    break;
                case HideSpriteCutsceneActionData hide:
                    names.Add(hide.EntityName!);
                    break;
                case TintSpriteCutsceneActionData tint:
                    names.Add(tint.EntityName);
                    seconds += tint.Duration ?? 0f;
                    break;
                case MoveSpriteCutsceneActionData move:
                    names.Add(move.EntityName);
                    seconds += System.Numerics.Vector2.Distance(from, new System.Numerics.Vector2(move.X!.Value, move.Y!.Value)) / move.Speed!.Value;
                    from = new System.Numerics.Vector2(move.X.Value, move.Y.Value);
                    break;
                case WaitCutsceneActionData wait:
                    seconds += wait.Seconds;
                    break;
            }
        }

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Equal("Marker", name));
        Assert.True(seconds >= 2f, $"The cutscene lasts {seconds} seconds.");
    }

    [Fact]
    public void EveryEntityTheLiveCutsceneNames_IsAStaticSpriteOfTheLiveWorld()
    {
        var entities = EntitiesOf();

        foreach (var action in Flatten(LoadCutscene().RootAction))
        {
            var name = action switch
            {
                ShowSpriteCutsceneActionData show => show.EntityName,
                HideSpriteCutsceneActionData hide => hide.EntityName,
                MoveSpriteCutsceneActionData move => move.EntityName,
                TintSpriteCutsceneActionData tint => tint.EntityName,
                _ => null,
            };

            if (name != null)
            {
                Assert.True(entities.ContainsKey(name), name);
                Assert.Equal("StaticSpriteComponent", (string?)entities[name]["root_component"]!["type"]);
            }
        }
    }

    [Fact]
    public void TheLiveWorld_NamesTheLiveCutsceneAsItsStartupCutsceneInTheCatalogue()
    {
        var catalog = Catalog();
        var source = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Cutscenes", "live.cutscene")));

        var info = Assert.Single(catalog, entry => (string?)entry["file_name"] == "Cutscenes\\live.cutscene");

        Assert.Equal("cutscene", (string?)info["asset_type"]);
        Assert.Equal((string?)source["id"], (string?)info["id"]);
        Assert.Equal((string?)info["id"], (string?)LoadWorld()["startup_cutscene_asset_id"]);
    }

    [Fact]
    public void TheLiveWorld_IsCataloguedWithItsOwnIdAndHoldsTheFiveEntitiesOfTheSample()
    {
        var catalog = Catalog();
        var world = LoadWorld();

        var info = Assert.Single(catalog, entry => (string?)entry["file_name"] == "LiveWorld.world");

        Assert.Equal("world", (string?)info["asset_type"]);
        Assert.Equal((string?)world["id"], (string?)info["id"]);
        Assert.Equal(LiveEntities.Order(), EntitiesOf().Keys.Where(name => name != "camera").Order());
    }

    [Fact]
    public void EveryEntityOfTheLiveWorld_LoadsWithItsComponentsAndTheBodiesFollowTheModelOfAnEntityWithPhysics()
    {
        var entities = EntitiesOf().ToDictionary(pair => pair.Key, pair => LoadEntity(pair.Value));

        Assert.IsType<AnimatedSpriteComponent>(entities["AnimatedOrb"].RootComponent);
        Assert.IsType<ParticleSystemComponent>(entities["Sparks"].RootComponent);
        Assert.IsType<StaticSpriteComponent>(entities["Marker"].RootComponent);
        Assert.False(entities["Marker"].IsVisible);

        // A dynamic body moves its entity only through its parent: the root component, with a null local pose.
        var box = entities["FallingBox"].RootComponent;
        var boxBody = Assert.IsType<CollisionComponent>(Assert.Single(box.Children));
        Assert.IsType<StaticSpriteComponent>(box);
        Assert.Equal(PhysicsType.Dynamic, boxBody.PhysicsType);
        Assert.Equal(Microsoft.Xna.Framework.Vector3.Zero, boxBody.LocalTransform.Position);

        var floor = Assert.IsType<CollisionComponent>(entities["Floor"].RootComponent);
        Assert.Equal(PhysicsType.Static, floor.PhysicsType);
    }

    [Fact]
    public void TheBoxStartsWellAboveTheFloorSoThatItIsStillFallingAfterOneSecond()
    {
        var entities = EntitiesOf().ToDictionary(pair => pair.Key, pair => LoadEntity(pair.Value));
        var box = entities["FallingBox"].RootComponent;
        var boxBody = (CollisionComponent)Assert.Single(box.Children);
        var floor = (CollisionComponent)entities["Floor"].RootComponent;

        var boxBottom = box.LocalTransform.Position.Y - boxBody.Fixtures[0].Shape.BoundingBox.Max.Y;
        var floorTop = floor.LocalTransform.Position.Y + floor.Fixtures[0].Shape.BoundingBox.Max.Y;
        var gap = boxBottom - floorTop;
        // Free fall under the default gravity: about 5 units in one second.
        var fall = -0.5f * new PhysicsEngineSettings().Gravity.Y;

        Assert.True(gap > 10f, $"The gap between the box and the floor is {gap} units.");
        Assert.True(gap > 2f * fall, $"The box falls {fall} units in one second, the gap is {gap} units.");
    }

    [Fact]
    public void TheOrbAnimation_LoadsAsALoopOfTwoImagesOfTheSameLengthNamingCataloguedSprites()
    {
        var catalog = Catalog();
        var animationInfo = Assert.Single(catalog, entry => (string?)entry["file_name"] == "Animations\\OrbPulse.anim2d");
        Assert.Equal("anim2d", (string?)animationInfo["asset_type"]);
        var animation = new Animation2dData();
        animation.Load(JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Animations", "OrbPulse.anim2d"))));

        Assert.Equal((string?)animationInfo["id"], animation.Id.ToString());
        Assert.Equal(AnimationType.Loop, animation.AnimationType);
        Assert.Equal(0.7f, animation.GetDurationSeconds(), 3);
        var keyframes = Assert.Single(animation.Tracks).SpriteKeyframes;
        Assert.Equal([0f, 0.35f, 0.7f], keyframes.Select(keyframe => keyframe.TimeSeconds));
        Assert.Equal(keyframes[0].Value, keyframes[2].Value);
        Assert.NotEqual(keyframes[0].Value, keyframes[1].Value);
        foreach (var keyframe in keyframes)
        {
            var spriteInfo = Assert.Single(catalog, entry => (string?)entry["id"] == keyframe.Value.ToString());
            Assert.Equal("sprite", (string?)spriteInfo["asset_type"]);
        }

        var entity = EntitiesOf()["AnimatedOrb"];
        Assert.Equal([animation.Id.ToString()], entity["root_component"]!["animations"]!.Select(token => (string?)token));
    }

    [Fact]
    public void TheSparksEffect_ValidatesAndEmitsForEverWithParticlesThatLiveAtLeastTwoSeconds()
    {
        var catalog = Catalog();
        var effectInfo = Assert.Single(catalog, entry => (string?)entry["file_name"] == "Particles\\Sparks.particle");
        Assert.Equal("particle", (string?)effectInfo["asset_type"]);
        var effect = new ParticleEffectAsset();
        effect.Load(JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Particles", "Sparks.particle"))));

        Assert.Empty(effect.Validate());
        var emitter = Assert.Single(effect.Emitters);
        Assert.True(emitter.Looping);
        Assert.True(emitter.Emission.RateOverTime > 0f);
        Assert.True(emitter.Initial.Lifetime.Min >= 2f);

        var sparks = EntitiesOf()["Sparks"]["root_component"]!;
        Assert.Equal((string?)effectInfo["id"], (string?)sparks["particle_effect_asset_id"]);
    }

    [Fact]
    public void EveryNewFileOfTheLiveWorld_IsCataloguedAndExists()
    {
        var catalog = Catalog();

        foreach (var file in new[] { "LiveWorld.world", "Cutscenes\\live.cutscene", "Animations\\OrbPulse.anim2d", "Particles\\Sparks.particle" })
        {
            var info = Assert.Single(catalog, entry => (string?)entry["file_name"] == file);
            Assert.True(File.Exists(Path.Combine(ProjectPath(), file)), file);
            Assert.Equal(Guid.Parse((string)info["id"]!).ToString(), (string?)info["id"]);
        }
    }

    [Fact]
    public void TheStartWorldOfTheSample_IsStillTheSpriteWorld()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "SpriteCutsceneSample.json")));

        Assert.Equal("SpriteWorld.world", (string?)project["FirstWorldLoaded"]);
    }

    private static Entity LoadEntity(JToken node)
    {
        var entity = new Entity();
        entity.Load((JObject)node);
        return entity;
    }

    private static IEnumerable<CutsceneActionData> Flatten(CutsceneActionData action)
    {
        yield return action;

        var children = action switch
        {
            SequenceCutsceneActionData sequence => sequence.Actions,
            ParallelCutsceneActionData parallel => parallel.Actions,
            RepeatCutsceneActionData repeat => repeat.Actions,
            AtCutsceneActionData at => at.Actions,
            _ => null,
        };

        if (children == null)
        {
            yield break;
        }

        foreach (var child in children)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static CutsceneAsset LoadCutscene()
    {
        var cutscene = new CutsceneAsset();
        cutscene.Load(JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Cutscenes", "live.cutscene"))));
        return cutscene;
    }

    private static JObject LoadWorld() => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "LiveWorld.world")));

    private static Dictionary<string, JToken> EntitiesOf()
        => LoadWorld()["entity_references"]!.ToDictionary(reference => (string)reference["entity"]!["name"]!, reference => reference["entity"]!);

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", "SpriteCutsceneSample");

    private static string FindRepositoryRoot()
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

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
