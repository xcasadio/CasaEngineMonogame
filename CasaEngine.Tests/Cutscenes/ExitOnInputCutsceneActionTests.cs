using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The ExitOnInput cutscene action: JSON, validation, the watch (press, hold, arming, replacement, end of the play),
/// and the frames of a press. A frame replays the order of the game: the coroutines (the watch among them), then the
/// update of the input component.
/// </summary>
public sealed class ExitOnInputCutsceneActionTests
{
    private const string WatchName = "CutsceneInputWatch";

    private static readonly KeyboardState Down = new(Keys.Space);

    // ---- JSON ----

    [Fact]
    public void SaveLoad_RoundTripsTheAction()
    {
        var action = new ExitOnInputCutsceneActionData { Mapping = "sample-input", NextCutscene = "next" };
        action.Inputs.Add("exit");
        action.Inputs.Add("skip");
        var asset = new CutsceneAsset { RootAction = action };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<ExitOnInputCutsceneActionData>(loaded.RootAction);
        Assert.Equal("sample-input", root.Mapping);
        Assert.Equal(["exit", "skip"], root.Inputs);
        Assert.Equal("next", root.NextCutscene);
        Assert.True(loaded.Validate().IsValid);
        Assert.Equal("ExitOnInput", node["root_action"]!["type"]!.ToString());
        Assert.Equal(2, ((JArray)node["root_action"]!["inputs"]!).Count);
        Assert.Equal("next", node["root_action"]!["next_cutscene"]!.ToString());
    }

    // ---- Validation ----

    [Fact]
    public void AWellFormedAction_IsValid()
    {
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"ExitOnInput\",\"mapping\":\"m\",\"inputs\":[\"a\"],\"next_cutscene\":\"n\"}")));
    }

    [Theory]
    [InlineData("{\"type\":\"ExitOnInput\",\"inputs\":[\"a\"],\"next_cutscene\":\"n\"}", "ExitOnInput.mapping is required.")]
    [InlineData("{\"type\":\"ExitOnInput\",\"mapping\":\"m\",\"inputs\":[\"a\"]}", "ExitOnInput.next_cutscene is required.")]
    [InlineData("{\"type\":\"ExitOnInput\",\"mapping\":\"m\",\"next_cutscene\":\"n\"}", "ExitOnInput.inputs must name at least one input.")]
    [InlineData("{\"type\":\"ExitOnInput\",\"mapping\":\"m\",\"inputs\":[],\"next_cutscene\":\"n\"}", "ExitOnInput.inputs must name at least one input.")]
    public void AMissingField_IsRefused(string json, string expected)
    {
        Assert.Equal(expected, Assert.Single(ValidateAction(JObject.Parse(json))).Message);
    }

    [Theory]
    [InlineData("\"mapping\":3,\"inputs\":[\"a\"],\"next_cutscene\":\"n\"", "mapping must be a non-empty string")]
    [InlineData("\"mapping\":\"\",\"inputs\":[\"a\"],\"next_cutscene\":\"n\"", "mapping must be a non-empty string")]
    [InlineData("\"mapping\":\"m\",\"inputs\":[\"a\"],\"next_cutscene\":false", "next_cutscene must be a non-empty string")]
    [InlineData("\"mapping\":\"m\",\"inputs\":\"a\",\"next_cutscene\":\"n\"", "inputs must be a list of non-empty strings")]
    [InlineData("\"mapping\":\"m\",\"inputs\":[\"a\",4],\"next_cutscene\":\"n\"", "inputs[1] must be a non-empty string")]
    [InlineData("\"mapping\":\"m\",\"inputs\":[\"\"],\"next_cutscene\":\"n\"", "inputs[0] must be a non-empty string")]
    public void AMalformedField_IsRefusedWithItsContext(string fields, string expected)
    {
        var message = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"ExitOnInput\"," + fields + "}"))).Message;

        Assert.StartsWith("ExitOnInput." + expected, message);
    }

    // ---- Editor ----

    [Fact]
    public void TheReadOnlyBuilder_ShowsTheFields()
    {
        var action = new ExitOnInputCutsceneActionData { Mapping = "m", NextCutscene = "n" };
        action.Inputs.Add("a");
        action.Inputs.Add("b");

        var node = CutsceneReadOnlyDocumentBuilder.Build(new CutsceneAsset { Name = "Doc", RootAction = action }).RootAction;

        Assert.Equal(CutsceneActionTypes.ExitOnInput, node.Type);
        Assert.Equal("m", Property(node, "mapping"));
        Assert.Equal("a, b", Property(node, "inputs"));
        Assert.Equal("n", Property(node, "next_cutscene"));
    }

    // ---- Failures ----

    [Fact]
    public void WithoutARunningGame_ThePlayFails()
    {
        var world = new World();
        world.CutsceneDirector.Play(Cutscene("Start", Arm("exit", "next")));

        world.CoroutineManager.Update(Context(1));

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("ExitOnInput needs a running game", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnUnresolvedWatch_FailsThePlayWithTheReason()
    {
        var rig = new Rig();
        rig.Play(Cutscene("Start", Arm("exit", "unknown-cutscene"), new WaitFramesCutsceneActionData { Frames = 5 }));

        rig.RunFrame(false);

        var snapshot = rig.World.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Equal("unknown next_cutscene 'unknown-cutscene'", snapshot.ActiveActionStopReason);
    }

    // ---- The watch ----

    [Fact]
    public void APress_StopsTheCutsceneAndPlaysTheNextOne_AtTheMeasuredFrames()
    {
        var rig = new Rig();
        rig.World.CoroutineManager.ThrowCoroutineExceptionsInDebug = true;
        rig.Play(Endless("Start", Arm("exit", "next")));

        // Frame 1 arms the watch, frame 2 is its first step. The input component reads the press at frame 4.
        rig.RunFrames(3, down: false);
        Assert.Equal(4L, rig.RunFrame(true));
        rig.RunFrames(6, down: true);

        // The world runs before the input component: the watch reads the input state of the previous frame. The
        // cutscene stops one frame after the press, and the next one takes its first step one frame after that.
        Assert.Equal([("Start", CutsceneRuntimeState.Stopped, 5L)], rig.Ended);
        Assert.Equal(6L, rig.NextFirstStepFrame);
        var snapshot = rig.World.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Playing, snapshot.State);
        Assert.Equal("next", snapshot.AssetName);
        Assert.DoesNotContain(snapshot.ActiveCoroutines, info => info.Name == WatchName);
    }

    [Fact]
    public void AHeldButton_ExitsOnce()
    {
        var rig = new Rig();
        rig.Play(Endless("Start", Arm("exit", "next")));

        rig.RunFrames(3, down: false);
        rig.RunFrames(20, down: true);

        Assert.Single(rig.Ended);
        Assert.Equal(6L, rig.NextFirstStepFrame);
    }

    [Fact]
    public void AButtonHeldBeforeTheArming_DoesNotTriggerUntilReleasedAndPressedAgain()
    {
        var rig = new Rig();
        rig.Keyboard.Update(Down);
        rig.Play(Endless("Start", Arm("exit", "next")));

        rig.RunFrames(10, down: true);
        Assert.Empty(rig.Ended);
        Assert.Null(rig.NextFirstStepFrame);
        Assert.True(rig.World.CutsceneDirector.IsPlaying);

        rig.RunFrame(false);
        Assert.Equal(12L, rig.RunFrame(true));
        rig.RunFrames(3, down: true);

        Assert.Equal([("Start", CutsceneRuntimeState.Stopped, 13L)], rig.Ended);
        Assert.Equal(14L, rig.NextFirstStepFrame);
    }

    [Fact]
    public void AButtonHeldBeforeAndDuringTheArming_DoesNotTriggerUntilReleasedAndPressedAgain()
    {
        var rig = new Rig();
        rig.Keyboard.Update(Down);
        rig.Play(Endless("Start", Arm("exit", "next")));

        // The arming frame (1) and the first step of the watch (2) both see the button down.
        rig.RunFrames(7, down: true);
        Assert.Empty(rig.Ended);
        Assert.Null(rig.NextFirstStepFrame);

        rig.RunFrames(2, down: false);
        Assert.Equal(10L, rig.RunFrame(true));
        rig.RunFrames(3, down: true);

        Assert.Equal([("Start", CutsceneRuntimeState.Stopped, 11L)], rig.Ended);
        Assert.Equal(12L, rig.NextFirstStepFrame);
    }

    [Fact]
    public void AButtonPressedOnTheArmingFrame_DoesNotTriggerUntilReleasedAndPressedAgain()
    {
        var rig = new Rig();
        rig.Play(Endless("Start", Arm("exit", "next")));

        // Frame 1: the coroutines arm the watch, then the input component sees the button go down.
        rig.RunFrames(6, down: true);
        Assert.Empty(rig.Ended);

        rig.RunFrame(false);
        rig.RunFrame(true);
        rig.RunFrame(true);

        Assert.Single(rig.Ended);
    }

    [Fact]
    public void ANewExitOnInput_ReplacesTheWatchInProgress()
    {
        var rig = new Rig();
        var start = new SequenceCutsceneActionData();
        start.Actions.Add(Arm("first", "next-first"));
        start.Actions.Add(new WaitFramesCutsceneActionData { Frames = 2 });
        start.Actions.Add(Arm("second", "next-second"));
        start.Actions.Add(Loop());
        rig.Play(new CutsceneAsset { Name = "Start", RootAction = start });

        rig.RunFrames(5, down: false);
        Assert.Single(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines, info => info.Name == WatchName);

        // The first input no longer exits; the second does.
        rig.Press("first");
        rig.RunFrames(4, down: true);
        Assert.Empty(rig.Ended);
        rig.Press("second");
        rig.RunFrame(false);
        rig.RunFrame(true);
        rig.RunFrames(3, down: true);

        Assert.Equal(CutsceneRuntimeState.Stopped, Assert.Single(rig.Ended).State);
        Assert.Equal("next-second", rig.World.CutsceneDirector.GetDebugSnapshot().AssetName);
    }

    [Fact]
    public void ACutsceneThatCompletes_LeavesNoWatchAndASubsequentPressStartsNothing()
    {
        var rig = new Rig();
        rig.Play(Cutscene("Start", Arm("exit", "next")));

        rig.RunFrame(false);

        Assert.Equal([("Start", CutsceneRuntimeState.Completed, 1L)], rig.Ended);
        Assert.Empty(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);

        rig.RunFrames(2, down: false);
        rig.RunFrames(6, down: true);

        Assert.Single(rig.Ended);
        Assert.Null(rig.NextFirstStepFrame);
        Assert.Equal("Start", rig.World.CutsceneDirector.GetDebugSnapshot().AssetName);
        Assert.Empty(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Stopped")]
    [InlineData("Replaced")]
    public void TheWatchIsStoppedBeforeEndedIsRaised_WhateverTheEnd(string end)
    {
        var rig = new Rig();
        var root = new SequenceCutsceneActionData();
        root.Actions.Add(Arm("exit", "next"));
        root.Actions.Add(new WaitFramesCutsceneActionData { Frames = 2 });
        if (end == "Failed")
        {
            root.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "Missing" });
        }

        if (end is "Stopped" or "Replaced")
        {
            root.Actions.Add(Loop());
        }

        var watchAtEnded = new List<bool>();
        rig.Play(new CutsceneAsset { Name = "Start", RootAction = root });
        rig.World.CutsceneDirector.Ended += (_, _) =>
            watchAtEnded.Add(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines.Any(info => info.Name == WatchName));

        rig.RunFrames(2, down: false);
        Assert.Single(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines, info => info.Name == WatchName);
        if (end == "Stopped")
        {
            rig.World.CutsceneDirector.Stop();
        }
        else if (end == "Replaced")
        {
            rig.World.CutsceneDirector.Play(Cutscene("Other", new WaitFramesCutsceneActionData { Frames = 9 }));
        }

        rig.RunFrames(3, down: false);

        Assert.Equal([false], watchAtEnded);
        Assert.DoesNotContain(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines, info => info.Name == WatchName);
    }

    [Fact]
    public void ThePlayOfTheNextCutsceneFromTheWatch_IsSafeWhileTheWatchRuns()
    {
        var rig = new Rig();
        rig.World.CoroutineManager.ThrowCoroutineExceptionsInDebug = true;
        rig.Play(Endless("Start", Arm("exit", "chain")));

        rig.RunFrames(3, down: false);
        rig.RunFrames(2, down: true);
        rig.RunFrames(3, down: false);

        // "chain" arms its own watch for another input: the first watch and its play are gone, the new ones stand.
        Assert.Equal(CutsceneRuntimeState.Stopped, Assert.Single(rig.Ended).State);
        Assert.Single(rig.World.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines, info => info.Name == WatchName);
        Assert.True(rig.World.CutsceneDirector.IsPlaying);

        rig.Press("chained");
        rig.RunFrames(3, down: true);

        Assert.Equal(2, rig.Ended.Count);
        Assert.Equal("next", rig.World.CutsceneDirector.GetDebugSnapshot().AssetName);
    }

    [Fact]
    public void TheStepOfTheWatch_DoesNotAllocate()
    {
        var world = new World();
        var keyboard = new KeyboardManager();
        var watch = new CutsceneInputWatch(
            world.CutsceneDirector,
            [NewInput("exit")],
            keyboard,
            new MouseManager(),
            new GamePadManager(),
            Cutscene("next", new WaitFramesCutsceneActionData { Frames = 1 }));
        var run = watch.Run();
        for (int i = 0; i < 20; i++)
        {
            keyboard.Update(new KeyboardState());
            run.MoveNext();
        }

        var before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            run.MoveNext();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // ---- Helpers ----

    private static string Property(CutsceneReadOnlyActionNode node, string name)
        => node.Properties.Single(property => property.Name == name).Value;

    private static CoroutineUpdateContext Context(long update) => new(1f, 1f, 1f, update);

    private static ExitOnInputCutsceneActionData Arm(string input, string next)
    {
        var action = new ExitOnInputCutsceneActionData { Mapping = "test-mapping", NextCutscene = next };
        action.Inputs.Add(input);
        return action;
    }

    private static RepeatCutsceneActionData Loop()
    {
        var repeat = new RepeatCutsceneActionData { Count = 0 };
        repeat.Actions.Add(new WaitFramesCutsceneActionData { Frames = 1 });
        return repeat;
    }

    private static CutsceneAsset Cutscene(string name, params CutsceneActionData[] actions)
    {
        var root = new SequenceCutsceneActionData();
        root.Actions.AddRange(actions);
        return new CutsceneAsset { Name = name, RootAction = root };
    }

    private static CutsceneAsset Endless(string name, params CutsceneActionData[] actions)
        => Cutscene(name, [.. actions, Loop()]);

    private static InputMapping NewInput(string name)
        => new()
        {
            Name = name,
            ButtonBehavior = ButtonBehaviors.DigitalInput,
            KeyButton = new KeyButton(Keys.None),
            AlternativeKeyButton = new KeyButton(),
        };

    private static IReadOnlyList<CutsceneValidationMessage> ValidateAction(JObject action)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = action };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.Validate().Messages;
    }

    /// <summary>A world, one visible entity, and the devices a watch reads; the frames replay the order of the game.</summary>
    private sealed class Rig
    {
        private readonly Dictionary<string, InputMapping> _inputs = [];
        private readonly Dictionary<string, CutsceneAsset> _cutscenes = [];
        private Keys _pressedKey = Keys.Space;

        public Rig()
        {
            World = new World();
            Entity = new Entity { Name = "Star", RootComponent = new StaticSpriteComponent() };
            World.Entities.Add(Entity);

            foreach (var (name, key) in new[] { ("exit", Keys.Space), ("first", Keys.A), ("second", Keys.B), ("chained", Keys.C) })
            {
                var input = NewInput(name);
                input.KeyButton = new KeyButton(key);
                _inputs[name] = input;
            }

            // The cutscenes played on a press hide the entity: their first step shows on the entity.
            _cutscenes["next"] = Endless("next", new HideSpriteCutsceneActionData { EntityName = "Star" });
            _cutscenes["next-first"] = Endless("next-first", new HideSpriteCutsceneActionData { EntityName = "Star" });
            _cutscenes["next-second"] = Endless("next-second", new HideSpriteCutsceneActionData { EntityName = "Star" });
            _cutscenes["chain"] = Endless("chain", Arm("chained", "next"));

            World.CutsceneDirector.ExitOnInputResolver = Resolve;
            World.CutsceneDirector.Ended += (_, args) => Ended.Add((args.Asset?.Name, args.State, Frame));
        }

        public World World { get; }

        public Entity Entity { get; }

        public KeyboardManager Keyboard { get; } = new();

        public long Frame { get; private set; }

        public List<(string Cutscene, CutsceneRuntimeState State, long Frame)> Ended { get; } = [];

        /// <summary>The frame of the first step of the cutscene played on a press (it hides the entity).</summary>
        public long? NextFirstStepFrame { get; private set; }

        public void Play(CutsceneAsset asset) => World.CutsceneDirector.Play(asset);

        /// <summary>The key held by the next frames: the one of this input.</summary>
        public void Press(string input) => _pressedKey = _inputs[input].KeyButton.Key;

        public long RunFrame(bool down)
        {
            Frame++;
            World.CoroutineManager.Update(Context(Frame));
            if (!Entity.IsVisible && NextFirstStepFrame == null)
            {
                NextFirstStepFrame = Frame;
            }

            // The input component updates after the world.
            Keyboard.Update(down ? new KeyboardState(_pressedKey) : new KeyboardState());
            return Frame;
        }

        public void RunFrames(int count, bool down)
        {
            for (int i = 0; i < count; i++)
            {
                RunFrame(down);
            }
        }

        private (CutsceneInputWatch, string) Resolve(ExitOnInputCutsceneActionData action)
        {
            if (!_cutscenes.TryGetValue(action.NextCutscene, out var next))
            {
                return (null, $"unknown next_cutscene '{action.NextCutscene}'");
            }

            var inputs = action.Inputs.Select(name => _inputs[name]).ToList();
            return (new CutsceneInputWatch(World.CutsceneDirector, inputs, Keyboard, new MouseManager(), new GamePadManager(), next), null);
        }
    }
}
