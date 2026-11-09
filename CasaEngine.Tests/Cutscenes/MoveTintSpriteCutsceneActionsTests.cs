using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>The MoveSprite and TintSprite cutscene actions: JSON, validation, execution over time and failures.</summary>
public sealed class MoveTintSpriteCutsceneActionsTests
{
    // ---- JSON ----

    [Fact]
    public void SaveLoad_RoundTripsMoveAndTint()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new MoveSpriteCutsceneActionData { EntityName = "Star", X = 12.5f, Y = -4f, Speed = 60f },
                    new TintSpriteCutsceneActionData { EntityName = "Star", Color = new Color(1, 2, 3, 4), Duration = 0.5f },
                },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        var move = Assert.IsType<MoveSpriteCutsceneActionData>(root.Actions[0]);
        Assert.Equal("Star", move.EntityName);
        Assert.Equal(12.5f, move.X);
        Assert.Equal(-4f, move.Y);
        Assert.Equal(60f, move.Speed);
        var tint = Assert.IsType<TintSpriteCutsceneActionData>(root.Actions[1]);
        Assert.Equal("Star", tint.EntityName);
        Assert.Equal(new Color(1, 2, 3, 4), tint.Color);
        Assert.Equal(0.5f, tint.Duration);
        Assert.True(loaded.Validate().IsValid);
        Assert.Equal("MoveSprite", node["root_action"]!["actions"]![0]!["type"]!.ToString());
        Assert.Equal("TintSprite", node["root_action"]!["actions"]![1]!["type"]!.ToString());
        Assert.Equal(4, ((JArray)node["root_action"]!["actions"]![1]!["color"]!).Count);
    }

    [Fact]
    public void Load_OfAThreeEntryColor_ReadsAlpha255()
    {
        var tint = Assert.IsType<TintSpriteCutsceneActionData>(
            LoadAction("{\"type\":\"TintSprite\",\"entity\":\"A\",\"color\":[10,20,30],\"duration\":1}"));

        Assert.Equal(new Color(10, 20, 30, 255), tint.Color);
    }

    [Fact]
    public void Load_OfIntegerAndDecimalNumbers_ReadsFloats()
    {
        var move = Assert.IsType<MoveSpriteCutsceneActionData>(
            LoadAction("{\"type\":\"MoveSprite\",\"entity\":\"A\",\"x\":3,\"y\":-2.5,\"speed\":10}"));

        Assert.Equal(3f, move.X);
        Assert.Equal(-2.5f, move.Y);
        Assert.Equal(10f, move.Speed);
    }

    // ---- Validator ----

    [Fact]
    public void Validate_OfAValidMoveAndTint_HasNoMessage()
    {
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"MoveSprite\",\"entity\":\"A\",\"x\":0,\"y\":0,\"speed\":1}")));
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"TintSprite\",\"entity\":\"A\",\"color\":[0,0,0],\"duration\":0}")));
    }

    [Theory]
    [InlineData("MoveSprite", "entity")]
    [InlineData("MoveSprite", "x")]
    [InlineData("MoveSprite", "y")]
    [InlineData("MoveSprite", "speed")]
    [InlineData("TintSprite", "entity")]
    [InlineData("TintSprite", "color")]
    [InlineData("TintSprite", "duration")]
    public void Validate_OfAMissingRequiredField_IsRefused(string type, string missing)
    {
        var action = type == "MoveSprite"
            ? JObject.Parse("{\"type\":\"MoveSprite\",\"entity\":\"A\",\"x\":0,\"y\":0,\"speed\":1}")
            : JObject.Parse("{\"type\":\"TintSprite\",\"entity\":\"A\",\"color\":[0,0,0],\"duration\":1}");
        action.Remove(missing);

        var error = Assert.Single(ValidateAction(action));

        Assert.Equal($"{type}.{missing} is required.", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void Validate_OfAMoveSpeedNotStrictlyPositive_IsRefused(string speed)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"MoveSprite\",\"entity\":\"A\",\"x\":0,\"y\":0,\"speed\":{speed}}}")));

        Assert.Equal("MoveSprite.speed must be greater than zero.", error.Message);
    }

    [Fact]
    public void Validate_OfANegativeTintDuration_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"TintSprite\",\"entity\":\"A\",\"color\":[0,0,0],\"duration\":-0.1}")));

        Assert.Equal("TintSprite.duration must be greater than or equal to zero.", error.Message);
    }

    [Theory]
    [InlineData("\"color\":{\"r\":1,\"g\":2,\"b\":3,\"a\":4}")]
    [InlineData("\"color\":[1,2]")]
    [InlineData("\"color\":[1,2,3,4,5]")]
    [InlineData("\"color\":[1,2,256]")]
    [InlineData("\"color\":[-1,2,3]")]
    [InlineData("\"color\":[1,2,3.5]")]
    public void Validate_OfAMalformedTintColor_IsRefused(string field)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"TintSprite\",\"entity\":\"A\",\"duration\":1,{field}}}")));

        Assert.StartsWith("TintSprite.color must be [r, g, b] or [r, g, b, a], each an integer from 0 to 255", error.Message);
    }

    [Fact]
    public void Validate_OfNonNumericAndMalformedFields_NamesEachField()
    {
        var errors = ValidateAction(JObject.Parse("{\"type\":\"MoveSprite\",\"entity\":3,\"x\":\"1\",\"y\":true,\"speed\":[]}"));

        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, error => error.Message.StartsWith("MoveSprite.entity must be a non-empty string"));
        Assert.Contains(errors, error => error.Message.StartsWith("MoveSprite.x must be a number"));
        Assert.Contains(errors, error => error.Message.StartsWith("MoveSprite.y must be a number"));
        Assert.Contains(errors, error => error.Message.StartsWith("MoveSprite.speed must be a number"));
    }

    [Fact]
    public void Validate_OfANonNumericTintDuration_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"TintSprite\",\"entity\":\"A\",\"color\":[0,0,0],\"duration\":\"1\"}")));

        Assert.StartsWith("TintSprite.duration must be a number", error.Message);
    }

    // ---- Execution ----

    [Fact]
    public void Move_StartsTheMotionAndLetsTheSequenceGoOnInTheSameUpdate()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");

        world.CutsceneDirector.Play(Asset(
            new MoveSpriteCutsceneActionData { EntityName = "Star", X = 100f, Y = 0f, Speed = 10f },
            new TintSpriteCutsceneActionData { EntityName = "Star", Color = Color.Red, Duration = 2f }));
        Step(world, 1, 0.1f);

        Assert.True(sprite.IsMoving);
        Assert.True(sprite.IsTinting);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void MoveThenWait_PutsTheSpriteWhereTheChosenTimeStepsSay()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        sprite.Position = new Vector3(0f, 0f, 3f);
        world.CutsceneDirector.Play(Asset(
            new MoveSpriteCutsceneActionData { EntityName = "Star", X = 100f, Y = 0f, Speed = 10f },
            new WaitCutsceneActionData { Seconds = 1f }));

        // Each frame runs the coroutines, then the entity update, as in World.Update.
        Frame(world, sprite, 1, 0.5f);
        Assert.Equal(5f, sprite.Position.X, 3);
        Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);

        Frame(world, sprite, 2, 0.5f);
        Assert.Equal(10f, sprite.Position.X, 3);
        Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);

        Frame(world, sprite, 3, 0.5f);
        Assert.Equal(15f, sprite.Position.X, 3);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(3f, sprite.Position.Z);
        Assert.True(sprite.IsMoving);
    }

    [Fact]
    public void Move_ArrivesExactlyOnTheTarget()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        world.CutsceneDirector.Play(Asset(
            new MoveSpriteCutsceneActionData { EntityName = "Star", X = 3f, Y = 4f, Speed = 100f },
            new WaitCutsceneActionData { Seconds = 1f }));

        Frame(world, sprite, 1, 0.5f);

        Assert.Equal(new Vector3(3f, 4f, 0f), sprite.Position);
        Assert.False(sprite.IsMoving);
    }

    [Fact]
    public void TintThenWait_ReachesHalfwayThenTheTarget()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        sprite.Color = new Color(0, 0, 0, 0);
        world.CutsceneDirector.Play(Asset(
            new TintSpriteCutsceneActionData { EntityName = "Star", Color = new Color(200, 100, 50, 255), Duration = 1f },
            new WaitCutsceneActionData { Seconds = 3f }));

        Frame(world, sprite, 1, 0.5f);
        Assert.Equal(new Color(100, 50, 25, 128), sprite.Color);
        Assert.True(sprite.IsTinting);

        Frame(world, sprite, 2, 0.5f);
        Assert.Equal(new Color(200, 100, 50, 255), sprite.Color);
        Assert.False(sprite.IsTinting);
    }

    [Fact]
    public void TintOfDurationZero_SetsTheColourAtOnce()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");

        PlayAndStep(world, new TintSpriteCutsceneActionData { EntityName = "Star", Color = Color.Blue, Duration = 0f });

        Assert.Equal(Color.Blue, sprite.Color);
        Assert.False(sprite.IsTinting);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ASecondMove_ReplacesTheFirst()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        world.CutsceneDirector.Play(Asset(
            new MoveSpriteCutsceneActionData { EntityName = "Star", X = 100f, Y = 0f, Speed = 10f },
            new MoveSpriteCutsceneActionData { EntityName = "Star", X = 0f, Y = 100f, Speed = 20f },
            new WaitCutsceneActionData { Seconds = 1f }));

        Frame(world, sprite, 1, 0.5f);

        Assert.Equal(0f, sprite.Position.X, 3);
        Assert.Equal(10f, sprite.Position.Y, 3);
    }

    // ---- Failures ----

    [Theory]
    [InlineData("Move")]
    [InlineData("Tint")]
    public void AnUnknownEntity_FailsThePlayNamingIt(string kind)
    {
        var (world, _, _) = CreateWorldWithSprite("Star");

        PlayAndStep(world, Action(kind, "Ghost"));

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Ghost'", snapshot.ActiveActionStopReason);
        Assert.Contains($"{kind}Sprite", snapshot.ActiveActionStopReason);
    }

    [Theory]
    [InlineData("Move")]
    [InlineData("Tint")]
    public void AnEntityWithoutASpriteRoot_FailsThePlayNamingIt(string kind)
    {
        var world = new World();
        world.Entities.Add(new Entity { Name = "Plain", RootComponent = new TestSceneComponent() });

        PlayAndStep(world, Action(kind, "Plain"));

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Plain'", snapshot.ActiveActionStopReason);
        Assert.Contains("StaticSpriteComponent", snapshot.ActiveActionStopReason);
    }

    // ---- Editor ----

    [Fact]
    public void TheReadOnlyBuilder_ShowsBothActionsAndTheirFields()
    {
        var asset = Asset(
            new MoveSpriteCutsceneActionData { EntityName = "Star", X = 1.5f, Y = 2f, Speed = 30f },
            new TintSpriteCutsceneActionData { EntityName = "Star", Color = new Color(1, 2, 3, 4), Duration = 0.25f });

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        var move = document.RootAction.Children[0];
        Assert.Equal(CutsceneActionTypes.MoveSprite, move.Type);
        Assert.Equal("Star", Property(move, "entity"));
        Assert.Equal("1.5", Property(move, "x"));
        Assert.Equal("2", Property(move, "y"));
        Assert.Equal("30", Property(move, "speed"));
        var tint = document.RootAction.Children[1];
        Assert.Equal(CutsceneActionTypes.TintSprite, tint.Type);
        Assert.Equal("Star", Property(tint, "entity"));
        Assert.Equal("1, 2, 3, 4", Property(tint, "color"));
        Assert.Equal("0.25", Property(tint, "duration"));
    }

    // ---- Helpers ----

    private static string Property(CutsceneReadOnlyActionNode node, string name)
        => node.Properties.Single(property => property.Name == name).Value;

    private static CutsceneActionData Action(string kind, string entity)
        => kind == "Move"
            ? new MoveSpriteCutsceneActionData { EntityName = entity, X = 1f, Y = 1f, Speed = 1f }
            : new TintSpriteCutsceneActionData { EntityName = entity, Color = Color.Red, Duration = 1f };

    private static CutsceneAsset Asset(params CutsceneActionData[] actions)
    {
        var root = new SequenceCutsceneActionData();
        root.Actions.AddRange(actions);
        return new CutsceneAsset { Name = "Sprites", RootAction = root };
    }

    private static (World World, Entity Entity, StaticSpriteComponent Sprite) CreateWorldWithSprite(string name)
    {
        var world = new World();
        var sprite = new StaticSpriteComponent();
        var entity = new Entity { Name = name, RootComponent = sprite };
        world.Entities.Add(entity);
        return (world, entity, sprite);
    }

    private static void PlayAndStep(World world, CutsceneActionData action)
    {
        world.CutsceneDirector.Play(Asset(action));
        Step(world, 1, 0.1f);
    }

    private static void Step(World world, long update, float deltaTime)
        => world.CoroutineManager.Update(new CoroutineUpdateContext(deltaTime, deltaTime, 1f, update));

    /// <summary>One frame in the order of World.Update: the coroutines first, then the entity's update.</summary>
    private static void Frame(World world, StaticSpriteComponent sprite, long update, float deltaTime)
    {
        Step(world, update, deltaTime);
        sprite.Update(deltaTime);
    }

    private static CutsceneActionData LoadAction(string json)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = JObject.Parse(json) };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.RootAction;
    }

    private static IReadOnlyList<CutsceneValidationMessage> ValidateAction(JObject action)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = action };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.Validate().Messages;
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
