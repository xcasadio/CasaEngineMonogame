using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>The Repeat cutscene action: JSON, validation, laps, the endless zero-frame guard and stopping.</summary>
public sealed class RepeatCutsceneActionTests
{
    // ---- JSON ----

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void SaveLoad_RoundTripsCountAndChildren(int count)
    {
        var repeat = new RepeatCutsceneActionData { Count = count };
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });
        var asset = new CutsceneAsset { RootAction = repeat };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<RepeatCutsceneActionData>(loaded.RootAction);
        Assert.Equal(count, root.Count);
        Assert.Equal(2, root.Actions.Count);
        Assert.IsType<WaitCutsceneActionData>(root.Actions[0]);
        Assert.IsType<HideSpriteCutsceneActionData>(root.Actions[1]);
        Assert.Equal("Repeat", node["root_action"]!["type"]!.ToString());
        Assert.Equal(JTokenType.Integer, node["root_action"]!["count"]!.Type);
        Assert.True(loaded.Validate().IsValid);
    }

    // ---- Validator ----

    [Fact]
    public void Validate_OfAValidRepeat_HasNoMessage()
    {
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"Repeat\",\"count\":0,\"actions\":[{\"type\":\"Wait\",\"seconds\":1}]}")));
    }

    [Fact]
    public void Validate_OfAMissingCount_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"Repeat\",\"actions\":[{\"type\":\"Wait\",\"seconds\":1}]}")));

        Assert.Equal(CutsceneValidationSeverity.Error, error.Severity);
        Assert.Equal("Repeat.count is required.", error.Message);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("\"3\"")]
    [InlineData("true")]
    [InlineData("null")]
    public void Validate_OfACountThatIsNotAnInteger_IsRefused(string count)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"Repeat\",\"count\":{count},\"actions\":[{{\"type\":\"Wait\",\"seconds\":1}}]}}")));

        Assert.StartsWith("Repeat.count must be an integer, but is ", error.Message);
    }

    [Fact]
    public void Validate_OfANegativeCount_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"Repeat\",\"count\":-1,\"actions\":[{\"type\":\"Wait\",\"seconds\":1}]}")));

        Assert.Equal(CutsceneValidationSeverity.Error, error.Severity);
        Assert.StartsWith("Repeat.count must be greater than or equal to zero", error.Message);
    }

    [Fact]
    public void Validate_OfAnEmptyList_WarnsLikeSequence()
    {
        var message = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"Repeat\",\"count\":2,\"actions\":[]}")));

        Assert.Equal(CutsceneValidationSeverity.Warning, message.Severity);
        Assert.Equal("Repeat action has no children.", message.Message);
    }

    [Fact]
    public void Validate_ChecksTheChildren()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"Repeat\",\"count\":2,\"actions\":[{\"type\":\"HideSprite\"}]}")));

        Assert.Equal("HideSprite.entity is required.", error.Message);
        Assert.Contains("actions[0]", error.Path);
    }

    // ---- Execution ----

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ACountedRepeat_TakesAsLongAsTheSameChildrenWrittenOutThatManyTimes(int count)
    {
        var repeatWorld = new World();
        var repeat = new RepeatCutsceneActionData { Count = count };
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        repeatWorld.CutsceneDirector.Play(new CutsceneAsset { Name = "Repeat", RootAction = repeat });

        var writtenWorld = new World();
        var written = new SequenceCutsceneActionData();
        for (int index = 0; index < count; index++)
        {
            written.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        }

        writtenWorld.CutsceneDirector.Play(new CutsceneAsset { Name = "Written", RootAction = written });

        int repeatFrames = FramesUntilEnded(repeatWorld, 1f);
        int writtenFrames = FramesUntilEnded(writtenWorld, 1f);

        Assert.True(repeatFrames > 0);
        Assert.Equal(writtenFrames, repeatFrames);
        Assert.Equal(CutsceneRuntimeState.Completed, repeatWorld.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ACountedRepeat_PlaysItsChildrenInOrderEveryLap()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        var repeat = new RepeatCutsceneActionData { Count = 2 };
        repeat.Actions.Add(new MoveSpriteCutsceneActionData { EntityName = "Star", X = 10f, Y = 0f, Speed = 1f });
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Order", RootAction = repeat });

        int starts = 0;
        for (long update = 1; update <= 20; update++)
        {
            bool movingBefore = sprite.IsMoving;
            Step(world, update, 1f);
            if (sprite.IsMoving && !movingBefore)
            {
                starts++;
            }

            sprite.StopMove();
        }

        // Each lap started one move; after the second lap the cutscene is over.
        Assert.Equal(2, starts);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ACountedRepeatOfInstantActions_RunsAllItsLapsInOneUpdate()
    {
        var (world, _, _) = CreateWorldWithSprite("Star");
        var repeat = new RepeatCutsceneActionData { Count = 5 };
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });

        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Instant", RootAction = repeat });
        Step(world, 1, 0.1f);

        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ACountedRepeatOfNothing_CompletesAtOnce()
    {
        var world = new World();

        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Empty", RootAction = new RepeatCutsceneActionData { Count = 1000000 } });
        Step(world, 1, 0.1f);

        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AnEndlessRepeat_KeepsPlayingLapAfterLap()
    {
        var world = new World();
        var repeat = new RepeatCutsceneActionData { Count = 0 };
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Endless", RootAction = repeat });

        for (long update = 1; update <= 50; update++)
        {
            Step(world, update, 1f);
            Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);
        }
    }

    [Fact]
    public void AnEndlessRepeat_AcceptsAFrameThatPassesInANestedAction()
    {
        var world = new World();
        var inner = new SequenceCutsceneActionData();
        inner.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        var repeat = new RepeatCutsceneActionData { Count = 0 };
        repeat.Actions.Add(inner);
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Nested", RootAction = repeat });

        for (long update = 1; update <= 10; update++)
        {
            Step(world, update, 1f);
        }

        Assert.Equal(CutsceneRuntimeState.Playing, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AnEndlessLapWithoutAFrame_FailsThePlayNamingTheAction()
    {
        var (world, _, _) = CreateWorldWithSprite("Star");
        var repeat = new RepeatCutsceneActionData { Count = 0 };
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 0f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Freeze", RootAction = repeat });

        Step(world, 1, 0.1f);

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("Repeat", snapshot.ActiveActionStopReason);
        Assert.Contains("without a frame passing", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnEndlessRepeatOfNothing_FailsThePlay()
    {
        var world = new World();

        world.CutsceneDirector.Play(new CutsceneAsset { Name = "EmptyEndless", RootAction = new RepeatCutsceneActionData { Count = 0 } });
        Step(world, 1, 0.1f);

        Assert.Equal(CutsceneRuntimeState.Failed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AFailingChild_EndsTheRepeatAndFailsThePlay()
    {
        var (world, _, _) = CreateWorldWithSprite("Star");
        var repeat = new RepeatCutsceneActionData { Count = 3 };
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Ghost" });
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Failing", RootAction = repeat });

        Step(world, 1, 0.1f);

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Ghost'", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void StoppingDuringALap_EndsTheCutsceneAndRunsNothingMore()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        var repeat = new RepeatCutsceneActionData { Count = 0 };
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 5f });
        repeat.Actions.Add(new MoveSpriteCutsceneActionData { EntityName = "Star", X = 10f, Y = 0f, Speed = 1f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Stop", RootAction = repeat });
        var ended = new List<CutsceneRuntimeState>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args.State);

        Step(world, 1, 1f);
        Step(world, 2, 1f);
        world.CutsceneDirector.Stop();
        for (long update = 3; update <= 12; update++)
        {
            Step(world, update, 1f);
        }

        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal([CutsceneRuntimeState.Stopped], ended);
        Assert.False(sprite.IsMoving);
        Assert.Empty(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);
    }

    // ---- Editor ----

    [Fact]
    public void TheReadOnlyBuilder_ShowsTheCountAndTheChildren()
    {
        var repeat = new RepeatCutsceneActionData { Count = 4 };
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });

        var document = CutsceneReadOnlyDocumentBuilder.Build(new CutsceneAsset { Name = "Doc", RootAction = repeat });

        var node = document.RootAction;
        Assert.Equal(CutsceneActionTypes.Repeat, node.Type);
        Assert.Equal("4", Property(node, "count"));
        Assert.Equal("1", Property(node, "children"));
        Assert.Single(node.Children);
    }

    // ---- Helpers ----

    private static string Property(CutsceneReadOnlyActionNode node, string name)
        => node.Properties.Single(property => property.Name == name).Value;

    private static (World World, Entity Entity, StaticSpriteComponent Sprite) CreateWorldWithSprite(string name)
    {
        var world = new World();
        var sprite = new StaticSpriteComponent();
        var entity = new Entity { Name = name, RootComponent = sprite };
        world.Entities.Add(entity);
        return (world, entity, sprite);
    }

    private static void Step(World world, long update, float deltaTime)
        => world.CoroutineManager.Update(new CoroutineUpdateContext(deltaTime, deltaTime, 1f, update));

    /// <summary>The number of steps until the cutscene is no longer playing (at most 100).</summary>
    private static int FramesUntilEnded(World world, float deltaTime)
    {
        for (int frame = 1; frame <= 100; frame++)
        {
            Step(world, frame, deltaTime);
            if (world.CutsceneDirector.GetDebugSnapshot().State != CutsceneRuntimeState.Playing)
            {
                return frame;
            }
        }

        return -1;
    }

    private static IReadOnlyList<CutsceneValidationMessage> ValidateAction(JObject action)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = action };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.Validate().Messages;
    }
}
