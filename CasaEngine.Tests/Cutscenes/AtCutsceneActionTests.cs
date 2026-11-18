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

/// <summary>The At cutscene action: JSON, validation, the delay, the children, the notifications and stopping.</summary>
public sealed class AtCutsceneActionTests
{
    private const float StepSeconds = 0.1f;

    // ---- JSON ----

    [Fact]
    public void SaveLoad_RoundTripsSecondsAndChildren()
    {
        var at = new AtCutsceneActionData { Seconds = 1.5f };
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        at.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(new CutsceneAsset { RootAction = at }, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<AtCutsceneActionData>(loaded.RootAction);
        Assert.Equal(1.5f, root.Seconds);
        Assert.Equal(2, root.Actions.Count);
        Assert.IsType<WaitCutsceneActionData>(root.Actions[0]);
        Assert.IsType<HideSpriteCutsceneActionData>(root.Actions[1]);
        Assert.Empty(root.FormatErrors);
        Assert.Equal("At", node["root_action"]!["type"]!.ToString());
        Assert.Equal(["type", "seconds", "actions"], node["root_action"]!.Children<JProperty>().Select(property => property.Name).ToArray());
        Assert.True(loaded.Validate().IsValid);
    }

    [Fact]
    public void ASavedThenLoadedThenSavedAt_GivesTheSameText()
    {
        var at = new AtCutsceneActionData { Seconds = 0.25f };
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        var first = new JObject();
        CutsceneAssetJsonSerializer.Save(new CutsceneAsset { RootAction = at }, first);

        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, first);
        var second = new JObject();
        CutsceneAssetJsonSerializer.Save(loaded, second);

        Assert.Equal(first["root_action"]!.ToString(), second["root_action"]!.ToString());
    }

    [Fact]
    public void ALoadedAtWithoutSeconds_HasZero_AndNoLoadLoss()
    {
        var source = Root(JObject.Parse("{\"type\":\"At\",\"actions\":[{\"type\":\"Wait\",\"seconds\":1}]}"));

        var session = CutsceneEditSession.Load(source);

        var at = Assert.IsType<AtCutsceneActionData>(session.Asset.RootAction);
        Assert.Equal(0f, at.Seconds);
        Assert.Empty(at.FormatErrors);
        Assert.Empty(session.LoadLosses);
        Assert.True(session.Asset.Validate().IsValid);
    }

    // ---- Validator ----

    [Fact]
    public void Validate_OfAValidAt_HasNoMessage()
    {
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"At\",\"seconds\":1.5,\"actions\":[{\"type\":\"Wait\",\"seconds\":1}]}")));
    }

    [Fact]
    public void Validate_OfANegativeSeconds_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"At\",\"seconds\":-0.5,\"actions\":[{\"type\":\"Wait\",\"seconds\":1}]}")));

        Assert.Equal(CutsceneValidationSeverity.Error, error.Severity);
        Assert.StartsWith("At.seconds must be greater than or equal to zero", error.Message);
    }

    [Fact]
    public void ASecondsThatIsNotANumber_LoadsWithoutThrowing_AndTheValidatorReportsAFormatError()
    {
        var node = Root(JObject.Parse("{\"type\":\"At\",\"seconds\":\"x\",\"actions\":[]}"));
        var asset = new CutsceneAsset();

        CutsceneAssetJsonSerializer.Load(asset, node);

        var at = Assert.IsType<AtCutsceneActionData>(asset.RootAction);
        Assert.Equal(0f, at.Seconds);
        Assert.True(at.FormatErrors.ContainsKey("seconds"));
        var errors = asset.Validate().Messages.Where(message => message.Severity == CutsceneValidationSeverity.Error).ToList();
        var error = Assert.Single(errors);
        Assert.StartsWith("At.seconds must be a number, but is ", error.Message);
    }

    [Fact]
    public void Validate_OfAnEmptyList_WarnsLikeSequence()
    {
        var message = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"At\",\"seconds\":1,\"actions\":[]}")));

        Assert.Equal(CutsceneValidationSeverity.Warning, message.Severity);
        Assert.Equal("At action has no children.", message.Message);
    }

    [Fact]
    public void Validate_ChecksTheChildren()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"At\",\"seconds\":1,\"actions\":[{\"type\":\"HideSprite\"}]}")));

        Assert.Equal("HideSprite.entity is required.", error.Message);
        Assert.Contains("actions[0]", error.Path);
    }

    // ---- Execution ----

    [Fact]
    public void AnAtOfOneSecondThenAWait_EndsWhenTheSameSequenceWrittenWithAWaitDoes()
    {
        var atWorld = new World();
        var at = new AtCutsceneActionData { Seconds = 1f };
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 0.5f });
        atWorld.CutsceneDirector.Play(new CutsceneAsset { Name = "At", RootAction = at });

        var writtenWorld = new World();
        var written = new SequenceCutsceneActionData();
        written.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });
        written.Actions.Add(new WaitCutsceneActionData { Seconds = 0.5f });
        writtenWorld.CutsceneDirector.Play(new CutsceneAsset { Name = "Written", RootAction = written });

        int atFrames = FramesUntilEnded(atWorld);
        int writtenFrames = FramesUntilEnded(writtenWorld);

        Assert.True(atFrames > 0);
        Assert.InRange(atFrames, writtenFrames - 1, writtenFrames + 1);
        Assert.Equal(CutsceneRuntimeState.Completed, atWorld.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AnAtOfZeroSeconds_LosesNoFrameComparedWithASequence()
    {
        var atWorld = new World();
        var at = new AtCutsceneActionData { Seconds = 0f };
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 0.5f });
        atWorld.CutsceneDirector.Play(new CutsceneAsset { Name = "At", RootAction = at });

        var sequenceWorld = new World();
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.Add(new WaitCutsceneActionData { Seconds = 0.5f });
        sequenceWorld.CutsceneDirector.Play(new CutsceneAsset { Name = "Sequence", RootAction = sequence });

        Assert.Equal(FramesUntilEnded(sequenceWorld), FramesUntilEnded(atWorld));
    }

    [Fact]
    public void AnAtOfZeroSecondsWithInstantChildren_CompletesInTheFirstUpdate()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        var at = new AtCutsceneActionData();
        at.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Star", X = 7f, Y = 0f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Instant", RootAction = at });

        Step(world, 1);

        Assert.Equal(7f, sprite.Position.X);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void TheChildrenOfAnAt_PlayInOrderAfterTheDelay()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        var at = new AtCutsceneActionData { Seconds = 0.5f };
        at.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Star", X = 1f, Y = 0f });
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 0.3f });
        at.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Star", X = 2f, Y = 0f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Order", RootAction = at });

        var trace = new List<float>();
        for (long update = 1; update <= 15; update++)
        {
            Step(world, update);
            if (trace.Count == 0 || trace[^1] != sprite.Position.X)
            {
                trace.Add(sprite.Position.X);
            }
        }

        Assert.Equal([0f, 1f, 2f], trace);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void TwoAtsInAParallel_StartTheirChildrenAtTheirOwnTimes()
    {
        var world = new World();
        var early = new StaticSpriteComponent();
        var late = new StaticSpriteComponent();
        world.Entities.Add(new Entity { Name = "Early", RootComponent = early });
        world.Entities.Add(new Entity { Name = "Late", RootComponent = late });
        var first = new AtCutsceneActionData { Seconds = 0.5f };
        first.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Early", X = 1f, Y = 0f });
        var second = new AtCutsceneActionData { Seconds = 1.5f };
        second.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Late", X = 1f, Y = 0f });
        var parallel = new ParallelCutsceneActionData { Actions = { first, second } };
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Cascade", RootAction = parallel });

        int earlyUpdate = 0;
        int lateUpdate = 0;
        for (int update = 1; update <= 30; update++)
        {
            Step(world, update);
            if (earlyUpdate == 0 && early.Position.X == 1f)
            {
                earlyUpdate = update;
            }

            if (lateUpdate == 0 && late.Position.X == 1f)
            {
                lateUpdate = update;
            }
        }

        Assert.InRange(earlyUpdate, 5, 7);
        Assert.InRange(lateUpdate, 15, 17);
        Assert.Equal(10, lateUpdate - earlyUpdate);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AnAt_RaisesItsNotificationsAroundThoseOfItsChildren()
    {
        var world = new World();
        var events = new List<string>();
        world.CutsceneDirector.ActionStarted += (in CutsceneActionNotification n) => events.Add("start:" + n.Action.Type);
        world.CutsceneDirector.ActionEnded += (in CutsceneActionNotification n) => events.Add("end:" + n.Action.Type);
        var at = new AtCutsceneActionData { Seconds = 0.2f };
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 0.2f });
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 0.2f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Notifications", RootAction = at });

        for (long update = 1; update <= 20; update++)
        {
            Step(world, update);
        }

        Assert.Equal(["start:At", "start:Wait", "end:Wait", "start:Wait", "end:Wait", "end:At"], events);
    }

    [Fact]
    public void StoppingDuringTheWait_RunsNothingMore_AndRaisesNoEndForTheAt()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        var events = new List<string>();
        world.CutsceneDirector.ActionEnded += (in CutsceneActionNotification n) => events.Add("end:" + n.Action.Type);
        var ended = new List<CutsceneRuntimeState>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args.State);
        var at = new AtCutsceneActionData { Seconds = 5f };
        at.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Star", X = 9f, Y = 0f });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Stop", RootAction = at });

        Step(world, 1);
        Step(world, 2);
        world.CutsceneDirector.Stop();
        for (long update = 3; update <= 80; update++)
        {
            Step(world, update);
        }

        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal([CutsceneRuntimeState.Stopped], ended);
        Assert.Empty(events);
        Assert.Equal(0f, sprite.Position.X);
        Assert.Empty(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);
    }

    [Fact]
    public void AnAtAsTheLastAction_EndsTheSequenceAndTheCutscene()
    {
        var (world, _, sprite) = CreateWorldWithSprite("Star");
        var at = new AtCutsceneActionData { Seconds = 0.3f };
        at.Actions.Add(new ShowSpriteCutsceneActionData { EntityName = "Star", X = 3f, Y = 0f });
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.Add(new WaitCutsceneActionData { Seconds = 0.2f });
        sequence.Actions.Add(at);
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Last", RootAction = sequence });

        int frames = FramesUntilEnded(world);

        Assert.True(frames > 0);
        Assert.Equal(3f, sprite.Position.X);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AFailingChild_EndsTheAtAndFailsThePlay()
    {
        var (world, _, _) = CreateWorldWithSprite("Star");
        var at = new AtCutsceneActionData { Seconds = 0.1f };
        at.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Ghost" });
        at.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Star" });
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Failing", RootAction = at });

        for (long update = 1; update <= 5; update++)
        {
            Step(world, update);
        }

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Ghost'", snapshot.ActiveActionStopReason);
    }

    // ---- Editor ----

    [Fact]
    public void TheReadOnlyBuilder_ShowsTheSecondsAndTheChildren()
    {
        var at = new AtCutsceneActionData { Seconds = 1.5f };
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 1f });

        var document = CutsceneReadOnlyDocumentBuilder.Build(new CutsceneAsset { Name = "Doc", RootAction = at });

        var node = document.RootAction;
        Assert.Equal(CutsceneActionTypes.At, node.Type);
        Assert.Equal("1.5", Property(node, "seconds"));
        Assert.Equal("1", Property(node, "children"));
        Assert.Single(node.Children);
    }

    // ---- Helpers ----

    private static string Property(CutsceneReadOnlyActionNode node, string name)
        => node.Properties.Single(property => property.Name == name).Value;

    private static JObject Root(JObject action) => new()
    {
        ["id"] = Guid.NewGuid().ToString(),
        ["name"] = "Fixture",
        ["type"] = "CutsceneAsset",
        ["version"] = 1,
        ["root_action"] = action,
    };

    private static (World World, Entity Entity, StaticSpriteComponent Sprite) CreateWorldWithSprite(string name)
    {
        var world = new World();
        var sprite = new StaticSpriteComponent();
        var entity = new Entity { Name = name, RootComponent = sprite };
        world.Entities.Add(entity);
        return (world, entity, sprite);
    }

    private static void Step(World world, long update)
        => world.CoroutineManager.Update(new CoroutineUpdateContext(StepSeconds, StepSeconds, 1f, update));

    /// <summary>The number of steps until the cutscene is no longer playing (at most 100).</summary>
    private static int FramesUntilEnded(World world)
    {
        for (int frame = 1; frame <= 100; frame++)
        {
            Step(world, frame);
            if (world.CutsceneDirector.GetDebugSnapshot().State != CutsceneRuntimeState.Playing)
            {
                return frame;
            }
        }

        return -1;
    }

    private static IReadOnlyList<CutsceneValidationMessage> ValidateAction(JObject action)
    {
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, Root(action));
        return asset.Validate().Messages;
    }
}
