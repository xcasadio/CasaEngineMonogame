using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class MoveCutsceneActionCommandTests
{
    private readonly EditorHistoryStack history = new();

    // root Sequence [ a, b, c, d, Parallel [ e, Repeat [ f ] ] ]
    private sealed class Tree
    {
        public readonly SequenceCutsceneActionData Root = new();
        public readonly ParallelCutsceneActionData Parallel = new();
        public readonly RepeatCutsceneActionData Repeat = new() { Count = 2 };
        public readonly WaitCutsceneActionData A = new() { Seconds = 1 };
        public readonly WaitCutsceneActionData B = new() { Seconds = 2 };
        public readonly WaitCutsceneActionData C = new() { Seconds = 3 };
        public readonly WaitCutsceneActionData D = new() { Seconds = 4 };
        public readonly WaitCutsceneActionData E = new() { Seconds = 5 };
        public readonly HideSpriteCutsceneActionData F = new() { EntityName = "orb" };
        public readonly CutsceneEditSession Session;

        public Tree()
        {
            Repeat.Actions.Add(F);
            Parallel.Actions.Add(E);
            Parallel.Actions.Add(Repeat);
            Root.Actions.AddRange([A, B, C, D, Parallel]);
            Session = new CutsceneEditSession(new CutsceneAsset { RootAction = Root });
        }
    }

    private static string Serialize(CutsceneEditSession session)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(session.Asset, node);
        return node["root_action"]!.ToString();
    }

    private static void AssertOrder(List<CutsceneActionData> actual, params CutsceneActionData[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
    }

    // A file whose Wait and Parallel hold values the loader rewrites: each gives a load loss.
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
    public void MoveForwardInTheSameContainer_CountsTheIndexBeforeTheMoveAndUndoesAndRedoesTheExactOrder()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        // Index 3 is the place just before d, among the children before the move.
        history.Execute(new MoveCutsceneActionCommand(t.Session, t.A, t.Root, 3));
        AssertOrder(t.Root.Actions, t.B, t.C, t.A, t.D, t.Parallel);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        AssertOrder(t.Root.Actions, t.B, t.C, t.A, t.D, t.Parallel);
    }

    [Fact]
    public void MoveBackwardInTheSameContainer_KeepsTheIndexAsGiven()
    {
        var t = new Tree();

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.D, t.Root, 1));
        AssertOrder(t.Root.Actions, t.A, t.D, t.B, t.C, t.Parallel);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
        history.Redo();
        AssertOrder(t.Root.Actions, t.A, t.D, t.B, t.C, t.Parallel);
    }

    [Fact]
    public void MoveToTheEndOfTheSameContainer_PutsTheActionLast()
    {
        var t = new Tree();

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.A, t.Root, 5));
        AssertOrder(t.Root.Actions, t.B, t.C, t.D, t.Parallel, t.A);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
    }

    [Fact]
    public void MoveToTheStartOfTheSameContainer_PutsTheActionFirst()
    {
        var t = new Tree();

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.C, t.Root, 0));
        AssertOrder(t.Root.Actions, t.C, t.A, t.B, t.D, t.Parallel);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
    }

    [Fact]
    public void MoveToAnotherContainer_KeepsTheInstanceAndRestoresBothListsOnUndo()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.B, t.Repeat, 0));
        AssertOrder(t.Root.Actions, t.A, t.C, t.D, t.Parallel);
        AssertOrder(t.Repeat.Actions, t.B, t.F);

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.F, t.Root, 4));
        AssertOrder(t.Root.Actions, t.A, t.C, t.D, t.Parallel, t.F);
        AssertOrder(t.Repeat.Actions, t.B);

        history.Undo();
        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
        AssertOrder(t.Repeat.Actions, t.F);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        history.Redo();
        AssertOrder(t.Root.Actions, t.A, t.C, t.D, t.Parallel, t.F);
        AssertOrder(t.Repeat.Actions, t.B);
    }

    [Fact]
    public void MoveAContainerWithItsSubtree_MovesTheSameInstanceAndItsChildren()
    {
        var t = new Tree();

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.Parallel, t.Root, 0));

        AssertOrder(t.Root.Actions, t.Parallel, t.A, t.B, t.C, t.D);
        AssertOrder(t.Parallel.Actions, t.E, t.Repeat);
        AssertOrder(t.Repeat.Actions, t.F);
        Assert.Equal("root_action.actions[0].actions[1].actions[0]", CutsceneActionTree.GetPath(t.Root, t.F));

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
    }

    [Fact]
    public void MoveIntoAContainerThatIsNotAParentOfTheSource_AcceptsEveryIndexUpToItsCount()
    {
        var t = new Tree();

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.A, t.Parallel, 2));

        AssertOrder(t.Parallel.Actions, t.E, t.Repeat, t.A);
        AssertOrder(t.Root.Actions, t.B, t.C, t.D, t.Parallel);
    }

    [Fact]
    public void MoveUpAndDown_UseTheIndicesOfAnExchangeWithTheNeighbour()
    {
        var t = new Tree();

        // Up: (parent, index - 1). Down: (parent, index + 2).
        history.Execute(new MoveCutsceneActionCommand(t.Session, t.C, t.Root, 1));
        AssertOrder(t.Root.Actions, t.A, t.C, t.B, t.D, t.Parallel);

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.A, t.Root, 2));
        AssertOrder(t.Root.Actions, t.C, t.A, t.B, t.D, t.Parallel);
    }

    [Fact]
    public void TheMovedInstanceIsTheSameAndItsContentIsUntouched()
    {
        var t = new Tree();
        var moved = t.B;

        history.Execute(new MoveCutsceneActionCommand(t.Session, moved, t.Repeat, 1));

        Assert.Same(moved, t.Repeat.Actions[1]);
        Assert.Equal(2, moved.Seconds);
        Assert.Equal("Move Wait", new MoveCutsceneActionCommand(t.Session, t.C, t.Root, 0).Description);
    }

    [Fact]
    public void TheRootIsRefused()
    {
        var t = new Tree();

        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.Root, t.Parallel, 0));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, t.Root, t.Parallel, 0));
    }

    [Fact]
    public void AnActionIsRefusedIntoItselfAndIntoItsDescendants()
    {
        var t = new Tree();

        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.Parallel, t.Parallel, 0));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.Parallel, t.Repeat, 0));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.Parallel, t.Repeat, 1));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, t.Parallel, t.Repeat, 0));
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.Parallel);
    }

    [Fact]
    public void ADestinationThatIsNotAContainerIsRefused()
    {
        var t = new Tree();

        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.B, 0));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.F, 0));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, t.A, t.B, 0));
    }

    [Fact]
    public void AnActionOrADestinationOutsideTheTreeIsRefused()
    {
        var t = new Tree();
        var foreign = new WaitCutsceneActionData();
        var foreignContainer = new SequenceCutsceneActionData();

        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, foreign, t.Root, 0));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, foreignContainer, 0));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, foreign, t.Root, 0));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, t.A, foreignContainer, 0));
    }

    [Fact]
    public void AnIndexOutsideTheChildListIsRefused()
    {
        var t = new Tree();

        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.Root, -1));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.Root, 6));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.Parallel, 3));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, t.A, t.Root, 6));
        Assert.Null(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.Root, 5));
    }

    [Fact]
    public void TheTwoIndicesThatChangeNothingAreRefused()
    {
        var t = new Tree();

        // c is at index 2: its own index and the index just after it leave the order as it is.
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.C, t.Root, 2));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.C, t.Root, 3));
        Assert.Throws<ArgumentException>(() => new MoveCutsceneActionCommand(t.Session, t.C, t.Root, 3));
        Assert.Null(MoveCutsceneActionCommand.GetRefusal(t.Session, t.C, t.Root, 1));
        Assert.Null(MoveCutsceneActionCommand.GetRefusal(t.Session, t.C, t.Root, 4));

        // The first child stays first at index 0 and 1; the last one stays last at its index and at the count.
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.Root, 0));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.A, t.Root, 1));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.Parallel, t.Root, 4));
        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.Parallel, t.Root, 5));

        // The same index in another container is a real move.
        Assert.Null(MoveCutsceneActionCommand.GetRefusal(t.Session, t.E, t.Root, 0));
    }

    [Fact]
    public void TheLoadLossesOfAMovedActionAreUnchanged()
    {
        var session = LoadWithLosses();
        var root = session.Asset.RootAction!;
        var parallel = CutsceneActionCatalog.GetChildren(root)[1];
        var lossesBefore = session.LoadLosses.ToList();
        Assert.Equal(3, lossesBefore.Count);
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reasonBefore));

        history.Execute(new MoveCutsceneActionCommand(session, parallel, root, 0));

        Assert.Same(parallel, CutsceneActionCatalog.GetChildren(root)[0]);
        Assert.Equal(lossesBefore, session.LoadLosses);
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reasonDuring));
        Assert.Equal(reasonBefore, reasonDuring);

        history.Undo();
        Assert.Same(parallel, CutsceneActionCatalog.GetChildren(root)[1]);
        Assert.Equal(lossesBefore, session.LoadLosses);

        // A lossy leaf moved into a container: each loss still names the same action instance.
        var lossyLeaf = CutsceneActionCatalog.GetChildren(root)[2];
        history.Execute(new MoveCutsceneActionCommand(session, lossyLeaf, parallel, 0));
        Assert.Equal(lossesBefore, session.LoadLosses);
        Assert.Same(lossyLeaf, CutsceneActionCatalog.GetChildren(parallel)[0]);
    }

    [Fact]
    public void ASeriesOfMovesLeavesAnAssetThatSavesAndReloadsIdentically()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.A, t.Root, 4));
        history.Execute(new MoveCutsceneActionCommand(t.Session, t.D, t.Repeat, 1));
        history.Execute(new MoveCutsceneActionCommand(t.Session, t.Parallel, t.Root, 0));

        string saved = Serialize(t.Session);
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(t.Session.Asset, node);
        var reloaded = CutsceneEditSession.Load(node);
        Assert.Equal(saved, Serialize(reloaded));
        Assert.Empty(reloaded.LoadLosses);

        history.Undo();
        history.Undo();
        history.Undo();
        Assert.Equal(before, Serialize(t.Session));
    }
}
