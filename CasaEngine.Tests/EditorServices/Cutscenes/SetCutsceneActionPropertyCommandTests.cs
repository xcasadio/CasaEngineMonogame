using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class SetCutsceneActionPropertyCommandTests
{
    [Fact]
    public void ExecuteThenUndo_RestoresAnActionLoadedWithAFormatError()
    {
        var session = Load(new JObject { ["type"] = "ShowSprite", ["entity"] = "orb", ["x"] = "abc" });
        var action = (ShowSpriteCutsceneActionData)session.Asset.RootAction!;
        var before = Snapshot(session);
        Assert.True(action.FormatErrors.ContainsKey("x"));

        var command = Command(session, action, "x", 1.5f);
        command.Execute();

        Assert.Equal(1.5f, action.X);
        Assert.False(action.FormatErrors.ContainsKey("x"));
        Assert.NotEqual(before, Snapshot(session));

        command.Undo();

        Assert.Null(action.X);
        Assert.True(action.FormatErrors.ContainsKey("x"));
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void ExecuteThenUndo_RestoresAMoveToLoadedWithoutEntity()
    {
        var session = Load(new JObject { ["type"] = "MoveTo" });
        var action = (MoveToCutsceneActionData)session.Asset.RootAction!;
        var before = Snapshot(session);

        var command = Command(session, action, "entity", "hero");
        command.Execute();
        Assert.Equal("hero", action.EntityName);

        command.Undo();

        Assert.Equal(string.Empty, action.EntityName);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void ExecuteThenUndo_RestoresAnOpenScreenLoadedWithAnEmptyTarget()
    {
        var session = Load(new JObject { ["type"] = "OpenScreen", ["target"] = string.Empty });
        var action = (OpenScreenCutsceneActionData)session.Asset.RootAction!;
        var before = Snapshot(session);
        Assert.Equal(string.Empty, action.Target);

        var command = Command(session, action, "target", "title");
        command.Execute();
        Assert.Equal("title", action.Target);

        command.Undo();

        Assert.Equal(string.Empty, action.Target);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void ExecuteThenUndo_RestoresAShowSpriteLoadedWithoutColor()
    {
        var session = Load(new JObject { ["type"] = "ShowSprite", ["entity"] = "orb" });
        var action = (ShowSpriteCutsceneActionData)session.Asset.RootAction!;
        var before = Snapshot(session);
        Assert.Null(action.Color);

        var command = Command(session, action, "color", new Color(1, 2, 3, 4));
        command.Execute();
        Assert.Equal(new Color(1, 2, 3, 4), action.Color);
        Assert.NotNull(Snapshot(session).Json["root_action"]!["color"]);

        command.Undo();

        Assert.Null(action.Color);
        Assert.Equal(before, Snapshot(session));
        Assert.Null(Snapshot(session).Json["root_action"]!["color"]);
    }

    [Fact]
    public void AListIsRestoredByUndo_AndWrittenAgainByRedo_ThroughTheHistoryStack()
    {
        var session = Load(new JObject
        {
            ["type"] = "ExitOnInput",
            ["mapping"] = "pad",
            ["inputs"] = new JArray("a", "b"),
            ["next_cutscene"] = "next",
        });
        var action = (ExitOnInputCutsceneActionData)session.Asset.RootAction!;
        var before = Snapshot(session);
        var history = new EditorHistoryStack();

        history.Execute(Command(session, action, "inputs", new List<string> { "c" }));
        Assert.Equal(["c"], action.Inputs);

        history.Undo();
        Assert.Equal(["a", "b"], action.Inputs);
        Assert.Equal(before, Snapshot(session));

        history.Redo();
        Assert.Equal(["c"], action.Inputs);

        history.Undo();
        Assert.Equal(["a", "b"], action.Inputs);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void ExecuteUndoExecute_GivesTheModifiedState()
    {
        var session = Load(new JObject { ["type"] = "Wait", ["seconds"] = 1.0 });
        var action = (WaitCutsceneActionData)session.Asset.RootAction!;
        var before = Snapshot(session);

        var command = Command(session, action, "seconds", 2.5f);
        command.Execute();
        var modified = Snapshot(session);
        command.Undo();
        command.Execute();

        Assert.Equal(2.5f, action.Seconds);
        Assert.Equal(modified, Snapshot(session));
        Assert.NotEqual(before, Snapshot(session));
    }

    [Fact]
    public void AFormatError_IsClearedByExecute_AndBackAfterUndo_AndClearedAgainByRedo_ThroughTheHistoryStack()
    {
        var session = Load(new JObject { ["type"] = "WaitFrames", ["frames"] = 2.5 });
        var action = (WaitFramesCutsceneActionData)session.Asset.RootAction!;
        var history = new EditorHistoryStack();
        Assert.False(session.Asset.Validate().IsValid);

        history.Execute(Command(session, action, "frames", 10));
        Assert.True(session.Asset.Validate().IsValid);
        Assert.Empty(action.FormatErrors);

        history.Undo();
        Assert.False(session.Asset.Validate().IsValid);
        Assert.Single(action.FormatErrors);

        history.Redo();
        Assert.True(session.Asset.Validate().IsValid);
        Assert.Empty(action.FormatErrors);
    }

    [Fact]
    public void ALoadLoss_IsRemovedByExecute_AndBackAfterUndo()
    {
        var session = Load(new JObject { ["type"] = "OpenScreen", ["target"] = 5 });
        var action = (OpenScreenCutsceneActionData)session.Asset.RootAction!;
        var loss = new CutsceneLoadLoss(action, "target", "root_action", "target is not a string.");
        var other = new CutsceneLoadLoss(action, "other", "root_action", "another loss.");
        session.AddLoadLoss(loss);
        session.AddLoadLoss(other);
        var history = new EditorHistoryStack();

        history.Execute(Command(session, action, "target", "title"));
        Assert.Equal([other], session.LoadLosses);

        history.Undo();
        Assert.Contains(loss, session.LoadLosses);
        Assert.Contains(other, session.LoadLosses);
        Assert.Equal(2, session.LoadLosses.Count);

        history.Redo();
        Assert.Equal([other], session.LoadLosses);
    }

    [Fact]
    public void AnEditOfAnotherField_KeepsTheFormatErrorAndTheLoadLossOfTheFirst()
    {
        var session = Load(new JObject { ["type"] = "ShowSprite", ["entity"] = "orb", ["x"] = "abc" });
        var action = (ShowSpriteCutsceneActionData)session.Asset.RootAction!;
        var loss = new CutsceneLoadLoss(action, "x", "root_action", "x is not a number.");
        session.AddLoadLoss(loss);

        Command(session, action, "y", 2f).Execute();

        Assert.True(action.FormatErrors.ContainsKey("x"));
        Assert.Equal([loss], session.LoadLosses);
    }

    [Fact]
    public void TheCommandKeepsItsOwnCopyOfAListValue()
    {
        var session = Load(new JObject { ["type"] = "ExitOnInput", ["inputs"] = new JArray("a") });
        var action = (ExitOnInputCutsceneActionData)session.Asset.RootAction!;
        var value = new List<string> { "c" };

        var command = Command(session, action, "inputs", value);
        value.Add("d");
        command.Execute();

        Assert.Equal(["c"], action.Inputs);
    }

    [Fact]
    public void TheCommandIsDescribed()
    {
        var session = Load(new JObject { ["type"] = "Wait" });

        var command = Command(session, session.Asset.RootAction!, "seconds", 1f);

        Assert.Equal("Set Wait.seconds", command.Description);
    }

    [Fact]
    public void TheConstructor_RefusesAPropertyOfAnotherActionTypeAndAValueOfTheWrongType()
    {
        var session = Load(new JObject { ["type"] = "Wait" });
        var wait = session.Asset.RootAction!;
        var seconds = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;
        var target = CutsceneActionPropertyEditor.GetProperties(CutsceneActionTypes.OpenScreen)[0];

        Assert.Throws<ArgumentException>(() => new SetCutsceneActionPropertyCommand(session, wait, target, "title"));
        Assert.Throws<ArgumentException>(() => new SetCutsceneActionPropertyCommand(session, wait, seconds, "1"));
        Assert.Throws<ArgumentException>(() => new SetCutsceneActionPropertyCommand(session, wait, seconds, null));
        Assert.Throws<ArgumentNullException>(() => new SetCutsceneActionPropertyCommand(session, wait, null!, 1f));
        Assert.Empty(session.LoadLosses);
    }

    private static CutsceneEditSession Load(JObject rootAction)
    {
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, new JObject { ["root_action"] = rootAction });
        return new CutsceneEditSession(asset);
    }

    private static SetCutsceneActionPropertyCommand Command(CutsceneEditSession session, CutsceneActionData action, string key, object? value)
        => new(session, action, CutsceneActionPropertyEditor.FindProperty(action, key)!, value);

    private static State Snapshot(CutsceneEditSession session)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(session.Asset, node);
        node.Remove("id");

        var messages = session.Asset.Validate().Messages.Select(message => $"{message.Severity} {message.Path} {message.Message}");
        return new State(node, string.Join("\n", messages));
    }

    private sealed record State(JObject Json, string Validation)
    {
        public bool Equals(State? other)
            => other != null && Validation == other.Validation && JToken.DeepEquals(Json, other.Json);

        public override int GetHashCode() => Validation.GetHashCode();

        public override string ToString() => Json.ToString(Formatting.None) + "\n" + Validation;
    }
}
