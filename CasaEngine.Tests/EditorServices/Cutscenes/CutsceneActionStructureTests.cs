using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneActionStructureTests
{
    private readonly EditorHistoryStack history = new();

    // root Sequence [ Wait, Parallel [ Wait, Repeat [ HideSprite ] ], MoveSprite ]
    private static CutsceneEditSession BuildSession(out SequenceCutsceneActionData root, out ParallelCutsceneActionData parallel, out RepeatCutsceneActionData repeat)
    {
        root = new SequenceCutsceneActionData();
        parallel = new ParallelCutsceneActionData();
        repeat = new RepeatCutsceneActionData { Count = 2 };
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "orb" });
        parallel.Actions.Add(new WaitCutsceneActionData { Seconds = 1 });
        parallel.Actions.Add(repeat);
        root.Actions.Add(new WaitCutsceneActionData { Seconds = 2 });
        root.Actions.Add(parallel);
        root.Actions.Add(new MoveSpriteCutsceneActionData { EntityName = "orb", X = 1, Y = 2, Speed = 3 });
        return new CutsceneEditSession(new CutsceneAsset { RootAction = root });
    }

    private static string Serialize(CutsceneEditSession session)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(session.Asset, node);
        return node["root_action"]!.ToString();
    }

    // A file whose Wait and Repeat hold values the loader rewrites: each gives a load loss.
    private static CutsceneEditSession LoadWithLosses()
    {
        var file = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test",
            ["type"] = nameof(CutsceneAsset),
            ["version"] = 1,
            ["root_action"] = new JObject
            {
                ["type"] = "Sequence",
                ["actions"] = new JArray
                {
                    new JObject { ["type"] = "Wait", ["seconds"] = 1 },
                    new JObject
                    {
                        ["type"] = "Parallel",
                        ["actions"] = new JArray
                        {
                            new JObject { ["type"] = "Wait", ["seconds"] = "2" },
                            new JObject { ["type"] = "Wait", ["seconds"] = "3" },
                        },
                    },
                    new JObject { ["type"] = "Wait", ["seconds"] = "4" },
                },
            },
        };
        return CutsceneEditSession.Load(file);
    }

    [Fact]
    public void FindParentAndPath_LocateNestedActions()
    {
        var session = BuildSession(out var root, out var parallel, out var repeat);
        var hide = repeat.Actions[0];

        Assert.True(CutsceneActionTree.TryFindParent(root, hide, out var parent, out int index));
        Assert.Same(repeat, parent);
        Assert.Equal(0, index);

        Assert.True(CutsceneActionTree.TryFindParent(root, parallel, out parent, out index));
        Assert.Same(root, parent);
        Assert.Equal(1, index);

        Assert.False(CutsceneActionTree.TryFindParent(root, root, out _, out _));
        Assert.False(CutsceneActionTree.TryFindParent(root, new WaitCutsceneActionData(), out _, out _));

        Assert.Equal("root_action", CutsceneActionTree.GetPath(root, root));
        Assert.Equal("root_action.actions[1]", CutsceneActionTree.GetPath(root, parallel));
        Assert.Equal("root_action.actions[1].actions[1].actions[0]", CutsceneActionTree.GetPath(root, hide));
        Assert.Null(CutsceneActionTree.GetPath(root, new WaitCutsceneActionData()));
        Assert.NotNull(session);
    }

    [Fact]
    public void InsertionTarget_AContainerReceivesTheActionAsItsLastChild()
    {
        BuildSession(out var root, out var parallel, out var repeat);

        Assert.True(CutsceneActionTree.TryGetInsertionTarget(root, parallel, out var parent, out int index, out _));
        Assert.Same(parallel, parent);
        Assert.Equal(2, index);

        Assert.True(CutsceneActionTree.TryGetInsertionTarget(root, repeat, out parent, out index, out _));
        Assert.Same(repeat, parent);
        Assert.Equal(1, index);
    }

    [Fact]
    public void InsertionTarget_ALeafReceivesTheActionJustAfterItself()
    {
        BuildSession(out var root, out var parallel, out var repeat);

        Assert.True(CutsceneActionTree.TryGetInsertionTarget(root, root.Actions[0], out var parent, out int index, out _));
        Assert.Same(root, parent);
        Assert.Equal(1, index);

        Assert.True(CutsceneActionTree.TryGetInsertionTarget(root, repeat.Actions[0], out parent, out index, out _));
        Assert.Same(repeat, parent);
        Assert.Equal(1, index);
    }

    [Fact]
    public void InsertionTarget_WithoutSelectionGoesAtTheEndOfAContainerRoot()
    {
        BuildSession(out var root, out _, out _);

        Assert.True(CutsceneActionTree.TryGetInsertionTarget(root, null, out var parent, out int index, out _));
        Assert.Same(root, parent);
        Assert.Equal(3, index);
    }

    [Fact]
    public void InsertionTarget_IsRefusedWithoutAContainerOrForAForeignAction()
    {
        var leafRoot = new WaitCutsceneActionData();

        Assert.False(CutsceneActionTree.TryGetInsertionTarget(leafRoot, null, out _, out _, out string error));
        Assert.NotEmpty(error);
        Assert.False(CutsceneActionTree.TryGetInsertionTarget(leafRoot, leafRoot, out _, out _, out error));
        Assert.NotEmpty(error);
        Assert.False(CutsceneActionTree.TryGetInsertionTarget(null, null, out _, out _, out error));

        BuildSession(out var root, out _, out _);
        Assert.False(CutsceneActionTree.TryGetInsertionTarget(root, new WaitCutsceneActionData(), out _, out _, out error));
    }

    [Fact]
    public void Insert_ExecutesUndoesAndRedoesAtTheRootAndInNestedContainers()
    {
        var session = BuildSession(out var root, out _, out var repeat);
        string before = Serialize(session);

        var atRoot = new WaitCutsceneActionData { Seconds = 9 };
        var nested = new WaitCutsceneActionData { Seconds = 8 };
        history.Execute(new InsertCutsceneActionCommand(session, root, 1, atRoot));
        history.Execute(new InsertCutsceneActionCommand(session, repeat, 1, nested));

        Assert.Same(atRoot, root.Actions[1]);
        Assert.Equal(4, root.Actions.Count);
        Assert.Same(nested, repeat.Actions[1]);

        history.Undo();
        Assert.Single(repeat.Actions);
        history.Undo();
        Assert.Equal(before, Serialize(session));

        history.Redo();
        history.Redo();
        Assert.Same(atRoot, root.Actions[1]);
        Assert.Same(nested, repeat.Actions[1]);
    }

    [Fact]
    public void Insert_RefusesALeafParentAndAnIndexOutsideTheList()
    {
        var session = BuildSession(out var root, out _, out _);

        Assert.Throws<ArgumentException>(() => new InsertCutsceneActionCommand(session, root.Actions[0], 0, new WaitCutsceneActionData()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InsertCutsceneActionCommand(session, root, 4, new WaitCutsceneActionData()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InsertCutsceneActionCommand(session, root, -1, new WaitCutsceneActionData()));
    }

    [Fact]
    public void Remove_ExecutesUndoesAndRedoesWithTheSameInstance()
    {
        var session = BuildSession(out var root, out var parallel, out var repeat);
        string before = Serialize(session);
        var hide = repeat.Actions[0];

        history.Execute(new RemoveCutsceneActionCommand(session, parallel));
        Assert.Equal(2, root.Actions.Count);
        history.Execute(new RemoveCutsceneActionCommand(session, root.Actions[0]));
        Assert.Single(root.Actions);

        history.Undo();
        history.Undo();
        Assert.Equal(before, Serialize(session));
        Assert.Same(parallel, root.Actions[1]);
        Assert.Same(hide, ((RepeatCutsceneActionData)parallel.Actions[1]).Actions[0]);

        history.Redo();
        Assert.DoesNotContain(parallel, root.Actions);
    }

    [Fact]
    public void Remove_ANestedLeafPutsItBackAtItsIndex()
    {
        var session = BuildSession(out _, out var parallel, out _);
        var first = parallel.Actions[0];
        var command = new RemoveCutsceneActionCommand(session, first);

        command.Execute();
        Assert.Single(parallel.Actions);
        command.Undo();

        Assert.Same(first, parallel.Actions[0]);
        Assert.Equal(2, parallel.Actions.Count);
    }

    [Fact]
    public void Remove_RefusesTheRootAndAForeignAction()
    {
        var session = BuildSession(out var root, out _, out _);

        Assert.NotNull(RemoveCutsceneActionCommand.GetRefusal(session, root));
        Assert.Throws<ArgumentException>(() => new RemoveCutsceneActionCommand(session, root));
        Assert.Throws<ArgumentException>(() => new RemoveCutsceneActionCommand(session, new WaitCutsceneActionData()));
        Assert.Null(RemoveCutsceneActionCommand.GetRefusal(session, root.Actions[0]));
    }

    [Fact]
    public void Remove_TakesTheLoadLossesOfTheSubtreeAndPutsThemBackInPlace()
    {
        var session = LoadWithLosses();
        Assert.Equal(3, session.LoadLosses.Count);
        var root = session.Asset.RootAction!;
        var parallel = CutsceneActionCatalog.GetChildren(root)[1];
        var lossesBefore = session.LoadLosses.ToList();
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reasonBefore));

        var command = new RemoveCutsceneActionCommand(session, parallel);
        command.Execute();

        var remaining = Assert.Single(session.LoadLosses);
        Assert.Same(lossesBefore[2], remaining);
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reasonDuring));
        Assert.NotEqual(reasonBefore, reasonDuring);

        command.Undo();

        Assert.Equal(lossesBefore, session.LoadLosses);
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reasonAfter));
        Assert.Equal(reasonBefore, reasonAfter);
    }

    [Fact]
    public void Remove_TheLastLossingActionLetsTheGuardPass()
    {
        var session = LoadWithLosses();
        var root = session.Asset.RootAction!;
        var lossesBefore = session.LoadLosses.ToList();

        history.Execute(new RemoveCutsceneActionCommand(session, CutsceneActionCatalog.GetChildren(root)[2]));
        history.Execute(new RemoveCutsceneActionCommand(session, CutsceneActionCatalog.GetChildren(root)[1]));

        Assert.Empty(session.LoadLosses);
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out _));

        history.Undo();
        history.Undo();
        Assert.Equal(lossesBefore, session.LoadLosses);
    }

    [Fact]
    public void Duplicate_InsertsACloneJustAfterTheSourceAndUndoRemovesIt()
    {
        var session = BuildSession(out var root, out var parallel, out _);
        string before = Serialize(session);

        var command = InsertCutsceneActionCommand.TryCreateDuplicate(session, parallel, out string error);

        Assert.NotNull(command);
        Assert.Empty(error);
        history.Execute(command);
        Assert.Equal(4, root.Actions.Count);
        Assert.NotSame(parallel, root.Actions[2]);
        Assert.Same(command.Action, root.Actions[2]);
        Assert.Equal("root_action.actions[1]", CutsceneActionTree.GetPath(root, parallel));
        var copy = (ParallelCutsceneActionData)root.Actions[2];
        Assert.NotSame(parallel.Actions, copy.Actions);
        Assert.Equal(parallel.Actions.Count, copy.Actions.Count);

        history.Undo();
        Assert.Equal(before, Serialize(session));
    }

    [Fact]
    public void Duplicate_IsRefusedForTheRootAndForMarkedSubtrees()
    {
        var session = BuildSession(out var root, out var parallel, out _);
        Assert.Null(InsertCutsceneActionCommand.TryCreateDuplicate(session, root, out string error));
        Assert.NotEmpty(error);

        var withError = new ParallelCutsceneActionData();
        var broken = new ShowSpriteCutsceneActionData();
        broken.FormatErrors["x"] = "not a number";
        withError.Actions.Add(broken);
        root.Actions.Add(withError);
        Assert.Null(InsertCutsceneActionCommand.TryCreateDuplicate(session, withError, out error));
        Assert.NotEmpty(error);
        Assert.Null(InsertCutsceneActionCommand.TryCreateDuplicate(session, broken, out _));

        root.Actions.Add(new UnknownCutsceneActionData("Dance"));
        Assert.Null(InsertCutsceneActionCommand.TryCreateDuplicate(session, root.Actions[^1], out _));

        Assert.NotNull(InsertCutsceneActionCommand.TryCreateDuplicate(session, parallel, out _));

        var lossy = LoadWithLosses();
        var lossyParallel = CutsceneActionCatalog.GetChildren(lossy.Asset.RootAction!)[1];
        Assert.Null(InsertCutsceneActionCommand.TryCreateDuplicate(lossy, lossyParallel, out error));
        Assert.NotEmpty(error);
        var cleanLeaf = CutsceneActionCatalog.GetChildren(lossy.Asset.RootAction!)[0];
        Assert.NotNull(InsertCutsceneActionCommand.TryCreateDuplicate(lossy, cleanLeaf, out _));
    }

    [Fact]
    public void ASeriesOfCommands_LeavesAnAssetThatSavesAndReloadsIdentically()
    {
        var session = BuildSession(out var root, out var parallel, out var repeat);

        history.Execute(new InsertCutsceneActionCommand(session, repeat, 1, CutsceneActionCatalog.Create("Wait")));
        history.Execute(InsertCutsceneActionCommand.TryCreateDuplicate(session, parallel, out _)!);
        history.Execute(new RemoveCutsceneActionCommand(session, root.Actions[0]));
        history.Execute(new InsertCutsceneActionCommand(session, root, 0, CutsceneActionCatalog.Create("Repeat")));
        history.Undo();
        history.Redo();

        string saved = Serialize(session);
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(session.Asset, node);
        var reloaded = CutsceneEditSession.Load(node);

        Assert.Equal(saved, Serialize(reloaded));
        Assert.Empty(reloaded.LoadLosses);
    }
}
