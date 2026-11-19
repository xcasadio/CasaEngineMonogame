using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

/// <summary>The structure operations of the cutscene editor with an At action: they take it as a container without any other change (P10).</summary>
public sealed class AtCutsceneActionContainerTests
{
    private readonly EditorHistoryStack history = new();

    // root Sequence [ a, b, at At(1.5) [ c, d ], e ]
    private sealed class Tree
    {
        public readonly SequenceCutsceneActionData Root = new();
        public readonly AtCutsceneActionData At = new() { Seconds = 1.5f };
        public readonly WaitCutsceneActionData A = new() { Seconds = 1 };
        public readonly WaitCutsceneActionData B = new() { Seconds = 2 };
        public readonly WaitCutsceneActionData C = new() { Seconds = 3 };
        public readonly WaitCutsceneActionData D = new() { Seconds = 4 };
        public readonly WaitCutsceneActionData E = new() { Seconds = 5 };
        public readonly CutsceneEditSession Session;

        public Tree()
        {
            At.Actions.AddRange([C, D]);
            Root.Actions.AddRange([A, B, At, E]);
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

    [Fact]
    public void AnAtIsAContainerTypeOfTheEditor()
    {
        var t = new Tree();

        Assert.True(WrapCutsceneActionCommand.IsContainerType(CutsceneActionTypes.At));
        Assert.Null(WrapCutsceneActionCommand.GetRefusal(t.Session, t.A, CutsceneActionTypes.At));
    }

    [Fact]
    public void WrappingInAnAtPutsAnAtOfZeroSecondsAtItsPlace()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var command = new WrapCutsceneActionCommand(t.Session, t.B, CutsceneActionTypes.At);
        history.Execute(command);

        var at = Assert.IsType<AtCutsceneActionData>(command.Container);
        Assert.Equal(0f, at.Seconds);
        AssertOrder(t.Root.Actions, t.A, at, t.At, t.E);
        AssertOrder(at.Actions, t.B);
        Assert.Equal("Wrap Wait in At", command.Description);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.At, t.E);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        AssertOrder(t.Root.Actions, t.A, at, t.At, t.E);
    }

    [Fact]
    public void WrappingAnAtKeepsItsSubtreeAndItsSeconds()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        var command = new WrapCutsceneActionCommand(t.Session, t.At, CutsceneActionTypes.Parallel);
        history.Execute(command);

        AssertOrder(t.Root.Actions, t.A, t.B, command.Container, t.E);
        AssertOrder(t.At.Actions, t.C, t.D);
        Assert.Equal(1.5f, t.At.Seconds);

        history.Undo();
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void MovingAnActionIntoAnAtAddsItAfterItsChildren()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        Assert.Null(MoveCutsceneActionCommand.GetRefusal(t.Session, t.E, t.At, t.At.Actions.Count));
        history.Execute(new MoveCutsceneActionCommand(t.Session, t.E, t.At, t.At.Actions.Count));

        AssertOrder(t.Root.Actions, t.A, t.B, t.At);
        AssertOrder(t.At.Actions, t.C, t.D, t.E);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.At, t.E);
        AssertOrder(t.At.Actions, t.C, t.D);
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void AnAtCannotBeMovedInsideItself()
    {
        var t = new Tree();

        Assert.NotNull(MoveCutsceneActionCommand.GetRefusal(t.Session, t.At, t.At, 0));
    }

    [Fact]
    public void UngroupingAnAtLeavesItsChildrenAtItsPlace_AndTheUndoGivesItBackWithItsSeconds()
    {
        var t = new Tree();
        string before = Serialize(t.Session);

        Assert.Null(UngroupCutsceneActionCommand.GetRefusal(t.Session, t.At));
        var command = new UngroupCutsceneActionCommand(t.Session, t.At);
        history.Execute(command);

        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.E);
        Assert.Equal("Ungroup At", command.Description);

        history.Undo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.At, t.E);
        AssertOrder(t.At.Actions, t.C, t.D);
        Assert.Equal(1.5f, t.At.Seconds);
        Assert.Equal(before, Serialize(t.Session));

        history.Redo();
        AssertOrder(t.Root.Actions, t.A, t.B, t.C, t.D, t.E);
    }

    [Fact]
    public void AnAtCanBeCopiedAndPastedWithItsChildren()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();

        Assert.Null(clipboard.TryCopy(t.Session, t.At));
        Assert.Equal(CutsceneActionTypes.At, clipboard.ContentType);

        var paste = Assert.IsType<AtCutsceneActionData>(clipboard.CreatePaste());
        Assert.NotSame(t.At, paste);
        Assert.Equal(1.5f, paste.Seconds);
        Assert.Equal(2, paste.Actions.Count);
        Assert.NotSame(t.C, paste.Actions[0]);
        Assert.Equal(3f, Assert.IsType<WaitCutsceneActionData>(paste.Actions[0]).Seconds);
        Assert.Equal(4f, Assert.IsType<WaitCutsceneActionData>(paste.Actions[1]).Seconds);

        string before = Serialize(t.Session);
        history.Execute(new InsertCutsceneActionCommand(t.Session, t.Root, t.Root.Actions.Count, paste));
        AssertOrder(t.Root.Actions, t.A, t.B, t.At, t.E, paste);

        history.Undo();
        Assert.Equal(before, Serialize(t.Session));
    }

    [Fact]
    public void ADuplicateOfAnAtIsAllowed()
    {
        var t = new Tree();

        Assert.Null(InsertCutsceneActionCommand.GetDuplicateRefusal(t.Session, t.At));
    }

    [Fact]
    public void TheDropCalculation_AcceptsAnActionIntoAnAt_AsItsLastChild()
    {
        var t = new Tree();
        var rows = new List<CutsceneDropRow>
        {
            new(t.Root, 0f, 20f, 0, true),
            new(t.A, 24f, 20f, 1, false),
            new(t.B, 48f, 20f, 1, false),
            new(t.At, 72f, 20f, 1, true),
            new(t.C, 96f, 20f, 2, false),
            new(t.D, 120f, 20f, 2, false),
            new(t.E, 144f, 20f, 1, false),
        };
        var area = new CutsceneDropArea(0f, 0f, 300f, 300f);

        // The middle of the At row, which is a container.
        var target = CutsceneDropTargetResolver.Resolve(t.Root, t.A, rows, 50f, 72f + 12f, area);

        Assert.Equal(CutsceneDropKind.Into, target.Kind);
        Assert.Same(t.At, target.Container);
        Assert.Equal(2, target.Index);
        Assert.True(target.Accepted);

        history.Execute(new MoveCutsceneActionCommand(t.Session, t.A, target.Container!, target.Index));
        AssertOrder(t.At.Actions, t.C, t.D, t.A);
    }

    [Fact]
    public void AWrappedAtSavesAndReloadsIdentically()
    {
        var t = new Tree();
        history.Execute(new WrapCutsceneActionCommand(t.Session, t.B, CutsceneActionTypes.At));
        history.Execute(new WrapCutsceneActionCommand(t.Session, t.At, CutsceneActionTypes.At));

        string saved = Serialize(t.Session);
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(t.Session.Asset, node);
        var reloaded = CutsceneEditSession.Load(node);

        Assert.Equal(saved, Serialize(reloaded));
        Assert.Empty(reloaded.LoadLosses);
    }
}
