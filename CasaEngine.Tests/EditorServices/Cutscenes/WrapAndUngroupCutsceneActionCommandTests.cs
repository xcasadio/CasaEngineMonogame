using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class WrapAndUngroupCutsceneActionCommandTests
{
    private readonly EditorHistoryStack history = new();

    // root Sequence [ a, b, Parallel [ c, d, e ], Repeat(3) [ f ], Sequence [] ]
    private sealed class Tree
    {
        public readonly SequenceCutsceneActionData Root = new();
        public readonly ParallelCutsceneActionData Parallel = new();
        public readonly RepeatCutsceneActionData Repeat = new() { Count = 3 };
        public readonly SequenceCutsceneActionData Empty = new();
        public readonly WaitCutsceneActionData A = new() { Seconds = 1 };
        public readonly WaitCutsceneActionData B = new() { Seconds = 2 };
        public readonly WaitCutsceneActionData C = new() { Seconds = 3 };
        public readonly WaitCutsceneActionData D = new() { Seconds = 4 };
        public readonly WaitCutsceneActionData E = new() { Seconds = 5 };
        public readonly HideSpriteCutsceneActionData F = new() { EntityName = "orb" };
        public readonly CutsceneEditSession Session;

        public Tree()
        {
            Parallel.Actions.AddRange([C, D, E]);
            Repeat.Actions.Add(F);
            Root.Actions.AddRange([A, B, Parallel, Repeat, Empty]);
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

    // A file whose Repeat holds a rewritten laps value and whose children hold rewritten values: each gives a load loss.
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
                    new JObject { ["type"] = "Wait", ["seconds"] = "1" },
                    new JObject
                    {
                        ["type"] = "Parallel",
                        ["actions"] = new JArray
                        {
                            new JObject { ["type"] = "Wait", ["seconds"] = "2" },
                            new JObject { ["type"] = "Wait", ["seconds"] = 3 },
                            new JObject { ["type"] = "Wait", ["seconds"] = "4" },
                        },
                    },
                },
            },
        };
        return CutsceneEditSession.Load(file);
    }

    [Theory]
    [InlineData("Sequence")]
    [InlineData("Parallel")]
    [InlineData("Repeat")]
    public void WrappingALeafPutsAContainerAtItsPlace(string type)
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var command = new WrapCutsceneActionCommand(t.Session, t.B, type);
        history.Execute(command);

        Assert.Equal(type, command.Container.Type);
        AssertOrder(t.Root.Actions, t.A, command.Container, t.Parallel, t.Repeat, t.Empty);
        AssertOrder(Children(command.Container), t.B);
        Assert.Equal($"Wrap Wait in {type}", command.Description);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        AssertOrder(t.Root.Actions, t.A, command.Container, t.Parallel, t.Repeat, t.Empty);
        AssertOrder(Children(command.Container), t.B);
    }

    [Theory]
    [InlineData("Sequence")]
    [InlineData("Parallel")]
    [InlineData("Repeat")]
    public void WrappingAContainerKeepsItsSubtree(string type)
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var command = new WrapCutsceneActionCommand(t.Session, t.Parallel, type);
        history.Execute(command);

        AssertOrder(t.Root.Actions, t.A, t.B, command.Container, t.Repeat, t.Empty);
        AssertOrder(Children(command.Container), t.Parallel);
        AssertOrder(t.Parallel.Actions, t.C, t.D, t.E);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
        AssertOrder(t.Parallel.Actions, t.C, t.D, t.E);
        Assert.Equal(before, Serialize(t.Session));
    }

    [Theory]
    [InlineData("Sequence")]
    [InlineData("Parallel")]
    [InlineData("Repeat")]
    public void WrappingTheRootMakesTheContainerTheNewRoot(string type)
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var command = new WrapCutsceneActionCommand(t.Session, t.Root, type);
        history.Execute(command);

        Assert.Same(command.Container, t.Session.Asset.RootAction);
        AssertOrder(Children(command.Container), t.Root);
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);

        history.Undo();
        Assert.Same(t.Root, t.Session.Asset.RootAction);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        Assert.Same(command.Container, t.Session.Asset.RootAction);
    }

    [Fact]
    public void WrappingInARepeatStartsItAtOneLap()
    {
        var t = new Tree();

        var command = new WrapCutsceneActionCommand(t.Session, t.A, "Repeat");

        Assert.Equal(1, Assert.IsType<RepeatCutsceneActionData>(command.Container).Count);
    }

    [Fact]
    public void AWrappedTreeSavesAndReloadsIdentically()
    {
        var t = new Tree();
        history.Execute(new WrapCutsceneActionCommand(t.Session, t.Parallel, "Sequence"));
        history.Execute(new WrapCutsceneActionCommand(t.Session, t.Root, "Parallel"));

        string saved = Serialize(t.Session);
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(t.Session.Asset, node);
        var reloaded = CutsceneEditSession.Load(node);

        Assert.Equal(saved, Serialize(reloaded));
        Assert.Empty(reloaded.LoadLosses);
    }

    [Fact]
    public void ATypeThatIsNotAContainerIsRefused()
    {
        var t = new Tree();

        Assert.NotNull(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, "Wait"));
        Assert.NotNull(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, "MoveSprite"));
        Assert.NotNull(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, "NotAType"));
        Assert.NotNull(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, ""));
        Assert.Throws<ArgumentException>(() => new WrapCutsceneActionCommand(t.Session, t.A, "Wait"));
        Assert.Null(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, "Sequence"));
        Assert.Null(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, "Parallel"));
        Assert.Null(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, "Repeat"));
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
    }

    [Fact]
    public void AnActionOutsideTheTreeCannotBeWrapped()
    {
        var t = new Tree();
        var foreign = new WaitCutsceneActionData();

        Assert.NotNull(WrapCutsceneActionCommand.GetRefusal(t.Session, foreign, "Sequence"));
        Assert.Throws<ArgumentException>(() => new WrapCutsceneActionCommand(t.Session, foreign, "Sequence"));
    }

    [Fact]
    public void WrappingKeepsTheLoadLossesOfTheWrappedSubtree()
    {
        var session = LoadWithLosses();
        var root = session.Asset.RootAction!;
        var parallel = CutsceneActionCatalog.GetChildren(root)[1];
        var lossesBefore = session.LoadLosses.ToList();
        Assert.Equal(3, lossesBefore.Count);

        history.Execute(new WrapCutsceneActionCommand(session, parallel, "Sequence"));
        Assert.Equal(lossesBefore, session.LoadLosses);

        history.Undo();
        Assert.Equal(lossesBefore, session.LoadLosses);
    }

    [Fact]
    public void UngroupingAContainerPutsItsChildrenAtItsPlaceInOrder()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var command = new UngroupCutsceneActionCommand(t.Session, t.Parallel);
        history.Execute(command);

        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.E, t.Repeat, t.Empty);
        Assert.Equal("Ungroup Parallel", command.Description);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
        AssertOrder(t.Parallel.Actions, t.C, t.D, t.E);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.E, t.Repeat, t.Empty);
        history.Undo();
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void UngroupingAnEmptyContainerRemovesIt()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        history.Execute(new UngroupCutsceneActionCommand(t.Session, t.Empty));
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat);
    }

    [Fact]
    public void UngroupingARepeatLosesItsLapsAndTheUndoBringsThemBack()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        history.Execute(new UngroupCutsceneActionCommand(t.Session, t.Repeat));
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.F, t.Empty);
        Assert.DoesNotContain("\"count\"", Serialize(t.Session));

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
        Assert.Equal(3, t.Repeat.Count);
        AssertOrder(t.Repeat.Actions, t.F);
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void UngroupingANestedContainerStaysInsideItsParent()
    {
        var t = new Tree();
        var inner = new SequenceCutsceneActionData();
        inner.Actions.Add(t.F);
        t.Repeat.Actions.Clear();
        t.Repeat.Actions.AddRange([inner]);
        string before = Serialize(t.Session);

        history.Execute(new UngroupCutsceneActionCommand(t.Session, inner));
        AssertOrder(t.Repeat.Actions, t.F);

        history.Undo();
        AssertOrder(t.Repeat.Actions, inner);
        AssertOrder(inner.Actions, t.F);
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void UngroupingIsRefusedForTheRootAndForAnActionThatHoldsNothing()
    {
        var t = new Tree();

        Assert.NotNull(UngroupCutsceneActionCommand.GetRefusal(t.Session, t.Root));
        Assert.NotNull(UngroupCutsceneActionCommand.GetRefusal(t.Session, t.A));
        Assert.NotNull(UngroupCutsceneActionCommand.GetRefusal(t.Session, t.F));
        Assert.NotNull(UngroupCutsceneActionCommand.GetRefusal(t.Session, new SequenceCutsceneActionData()));
        Assert.Throws<ArgumentException>(() => new UngroupCutsceneActionCommand(t.Session, t.Root));
        Assert.Throws<ArgumentException>(() => new UngroupCutsceneActionCommand(t.Session, t.A));
        Assert.Null(UngroupCutsceneActionCommand.GetRefusal(t.Session, t.Parallel));
        Assert.Null(UngroupCutsceneActionCommand.GetRefusal(t.Session, t.Empty));
    }

    [Fact]
    public void WrappingThenUngroupingRestoresTheTree()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var wrap = new WrapCutsceneActionCommand(t.Session, t.B, "Parallel");
        history.Execute(wrap);
        history.Execute(new UngroupCutsceneActionCommand(t.Session, wrap.Container));

        AssertOrder(t.Root.Actions, t.A, t.B, t.Parallel, t.Repeat, t.Empty);
        Assert.Equal(before, Serialize(t.Session));

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, wrap.Container, t.Parallel, t.Repeat, t.Empty);
        history.Undo();
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void UngroupingMovesTheLossesOfTheContainerOnlyAndTheUndoPutsThemBackAtTheirPlace()
    {
        var session = LoadWithLosses();
        var root = session.Asset.RootAction!;
        var parallel = CutsceneActionCatalog.GetChildren(root)[1];
        var lossesBefore = session.LoadLosses.ToList();
        Assert.Equal(3, lossesBefore.Count);
        Assert.Contains(lossesBefore, loss => ReferenceEquals(loss.Action, CutsceneActionCatalog.GetChildren(parallel)[0]));

        // The Parallel itself carries a loss once its entry count differs from the file.
        var extra = new CutsceneLoadLoss(parallel, "actions", "root_action.actions[1]", "test loss of the container");
        session.InsertLoadLoss(1, extra);
        var allBefore = session.LoadLosses.ToList();
        Assert.Equal(4, allBefore.Count);

        history.Execute(new UngroupCutsceneActionCommand(session, parallel));

        Assert.DoesNotContain(extra, session.LoadLosses);
        Assert.Equal(allBefore.Where(loss => !ReferenceEquals(loss, extra)).ToList(), session.LoadLosses);
        Assert.Equal(4, CutsceneActionCatalog.GetChildren(root).Count);

        history.Undo();
        Assert.Equal(allBefore, session.LoadLosses);
        Assert.Same(parallel, CutsceneActionCatalog.GetChildren(root)[1]);

        history.Redo();
        Assert.DoesNotContain(extra, session.LoadLosses);
    }

    private static List<CutsceneActionData> Children(CutsceneActionData container)
        => (List<CutsceneActionData>)CutsceneActionCatalog.GetChildren(container);
}
