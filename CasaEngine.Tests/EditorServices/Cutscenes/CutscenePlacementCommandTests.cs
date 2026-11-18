using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

/// <summary>The commands that place a cutscene action at a time and add one at a time (P5, P9).</summary>
public sealed class CutscenePlacementCommandTests
{
    private readonly EditorHistoryStack history = new();

    private static SequenceCutsceneActionData Sequence(params CutsceneActionData[] actions)
    {
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.AddRange(actions);
        return sequence;
    }

    private static ParallelCutsceneActionData Parallel(params CutsceneActionData[] actions)
    {
        var parallel = new ParallelCutsceneActionData();
        parallel.Actions.AddRange(actions);
        return parallel;
    }

    private static AtCutsceneActionData At(float seconds, params CutsceneActionData[] actions)
    {
        var at = new AtCutsceneActionData { Seconds = seconds };
        at.Actions.AddRange(actions);
        return at;
    }

    private static WaitCutsceneActionData Wait(float seconds) => new() { Seconds = seconds };

    private static CutsceneEditSession Session(CutsceneActionData root) => new(new CutsceneAsset { RootAction = root });

    private static string Serialize(CutsceneEditSession session)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(session.Asset, node);
        return node.ToString();
    }

    private static void AssertOrder(List<CutsceneActionData> actual, params CutsceneActionData[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
    }

    private IEditorCommand Place(CutsceneEditSession session, CutsceneActionData action, double seconds)
    {
        var command = CutscenePlacement.TryCreatePlace(session, action, seconds, out string error);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(command);
        history.Execute(command);
        return command;
    }

    // ---- place: change the At ----

    [Fact]
    public void PlacingTheFirstChildOfAnAt_WritesTheSecondsOfThatAt_InOneHistoryEntry()
    {
        var x = Wait(1);
        var at = At(0.5f, x);
        var other = Wait(2);
        var root = Parallel(at, other);
        var session = Session(root);
        string before = Serialize(session);

        var command = Place(session, x, 1.2);

        Assert.Equal(1.2f, at.Seconds);
        AssertOrder(root.Actions, at, other);
        AssertOrder(at.Actions, x);
        Assert.Equal("Place Wait at 1.2 s", command.Description);

        history.Undo();
        Assert.Equal(0.5f, at.Seconds);
        Assert.Equal(before, Serialize(session));
        Assert.False(history.CanUndo);

        history.Redo();
        Assert.Equal(1.2f, at.Seconds);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void PlacingAtTheCurrentSeconds_IsRefusedAsWithoutEffect()
    {
        var x = Wait(1);
        var session = Session(Parallel(At(0.5f, x)));

        Assert.Null(CutscenePlacement.TryCreatePlace(session, x, 0.5, out string error));
        Assert.Equal("The action is already at this time.", error);
        Assert.NotNull(CutscenePlacement.GetPlaceRefusal(session, x, 0.5));
        Assert.Null(CutscenePlacement.GetPlaceRefusal(session, x, 0.6));
    }

    [Fact]
    public void PlacingAtZero_ChangesAnAtThatIsLate()
    {
        var x = Wait(1);
        var at = At(2, x);
        var session = Session(Parallel(at));

        Place(session, x, 0);

        Assert.Equal(0f, at.Seconds);
    }

    [Fact]
    public void PlacingTheFirstActionOfASequenceInAnAt_ChangesThatAt()
    {
        var x = Wait(1);
        var y = Wait(2);
        var at = At(1, Sequence(x, y));
        var session = Session(Parallel(at));

        Place(session, x, 3);

        Assert.Equal(3f, at.Seconds);
        Assert.Null(CutscenePlacement.TryCreatePlace(session, y, 4, out _));
    }

    // ---- place: wrap in an At ----

    [Fact]
    public void PlacingAChildOfAParallel_WrapsItInAnAt_KeepingTheInstanceAndItsPlace()
    {
        var a = Wait(1);
        var b = Wait(2);
        var c = Wait(3);
        var root = Parallel(a, b, c);
        var session = Session(root);
        string before = Serialize(session);

        var command = Place(session, b, 0.7);

        AssertOrder(root.Actions, a, root.Actions[1], c);
        var at = Assert.IsType<AtCutsceneActionData>(root.Actions[1]);
        Assert.Equal(0.7f, at.Seconds);
        AssertOrder(at.Actions, b);
        Assert.Equal("Place Wait at 0.7 s", command.Description);

        history.Undo();
        AssertOrder(root.Actions, a, b, c);
        Assert.Equal(before, Serialize(session));
        Assert.False(history.CanUndo);

        history.Redo();
        Assert.Same(at, root.Actions[1]);
        Assert.Equal(0.7f, at.Seconds);
        AssertOrder(at.Actions, b);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void AWrapAtZeroSeconds_IsAllowed()
    {
        var a = Wait(1);
        var root = Parallel(a);
        var session = Session(root);

        Place(session, a, 0);

        var at = Assert.IsType<AtCutsceneActionData>(Assert.Single(root.Actions));
        Assert.Equal(0f, at.Seconds);
        AssertOrder(at.Actions, a);

        history.Undo();
        AssertOrder(root.Actions, a);
    }

    [Fact]
    public void ADragOfTheFirstActionOfASequenceInAParallel_WrapsTheSequence_ThenTheSecondDragChangesTheAt()
    {
        var x = Wait(1);
        var y = Wait(2);
        var sequence = Sequence(x, y);
        var root = Parallel(sequence);
        var session = Session(root);

        Place(session, x, 0.4);
        var at = Assert.IsType<AtCutsceneActionData>(Assert.Single(root.Actions));
        Assert.Equal(0.4f, at.Seconds);
        AssertOrder(at.Actions, sequence);

        Place(session, x, 1.5);
        Assert.Same(at, root.Actions[0]);
        Assert.Equal(1.5f, at.Seconds);

        history.Undo();
        Assert.Equal(0.4f, at.Seconds);
        history.Undo();
        AssertOrder(root.Actions, sequence);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void APlacedTree_SavedThenReloaded_IsIdentical()
    {
        var a = Wait(1);
        var b = Wait(2);
        var session = Session(Parallel(a, b));
        Place(session, a, 0.25);
        Place(session, b, 1.5);
        string saved = Serialize(session);

        var node = JObject.Parse(saved);
        var reloaded = CutsceneEditSession.Load(node);

        Assert.Equal(saved, Serialize(reloaded));
        Assert.Empty(reloaded.LoadLosses);
        var root = Assert.IsType<ParallelCutsceneActionData>(reloaded.Asset.RootAction);
        Assert.Equal(0.25f, Assert.IsType<AtCutsceneActionData>(root.Actions[0]).Seconds);
        Assert.Equal(1.5f, Assert.IsType<AtCutsceneActionData>(root.Actions[1]).Seconds);
    }

    // ---- place: refusals ----

    [Fact]
    public void Placing_IsRefusedWhenNoDragMovesTheAction()
    {
        var first = Wait(1);
        var second = Wait(2);
        var inAt = Wait(3);
        var root = Sequence(first, second, At(0, Wait(9), inAt));
        var session = Session(root);
        string before = Serialize(session);

        foreach (var action in new CutsceneActionData[] { root, second, inAt })
        {
            Assert.Null(CutscenePlacement.TryCreatePlace(session, action, 1, out string error));
            Assert.NotEqual(string.Empty, error);
        }

        Assert.Equal(before, Serialize(session));
    }

    [Fact]
    public void Placing_IsRefusedForAnActionOutsideTheTreeAndForABadTime()
    {
        var x = Wait(1);
        var session = Session(Parallel(x));

        Assert.Equal("The action is not in the cutscene.", CutscenePlacement.GetPlaceRefusal(session, Wait(5), 1));
        Assert.NotNull(CutscenePlacement.GetPlaceRefusal(session, x, -0.1));
        Assert.NotNull(CutscenePlacement.GetPlaceRefusal(session, x, double.NaN));
        Assert.NotNull(CutscenePlacement.GetPlaceRefusal(session, x, double.PositiveInfinity));
        Assert.Null(CutscenePlacement.GetPlaceRefusal(session, x, 1));

        var empty = new CutsceneEditSession(new CutsceneAsset());
        Assert.Equal("The action is not in the cutscene.", CutscenePlacement.GetPlaceRefusal(empty, x, 1));
    }

    // ---- target of an addition ----

    [Fact]
    public void TheTarget_IsTheSelectedParallel()
    {
        var inner = Parallel(Wait(1));
        var root = Parallel(inner);

        Assert.Same(inner, CutscenePlacement.FindTargetParallel(root, inner));
        Assert.Same(root, CutscenePlacement.FindTargetParallel(root, root));
    }

    [Fact]
    public void TheTarget_OfAnActionInAnAtInAParallel_IsThatParallel()
    {
        var x = Wait(1);
        var root = Parallel(At(1, x));

        Assert.Same(root, CutscenePlacement.FindTargetParallel(root, x));
    }

    [Fact]
    public void TheTarget_IsTheNearestParallel()
    {
        var x = Wait(1);
        var inner = Parallel(Sequence(x));
        var root = Parallel(inner);

        Assert.Same(inner, CutscenePlacement.FindTargetParallel(root, x));
    }

    [Fact]
    public void TheTarget_IsNullWithoutAParallelParentOrASelection()
    {
        var x = Wait(1);
        var root = Sequence(x, Wait(2));

        Assert.Null(CutscenePlacement.FindTargetParallel(root, x));
        Assert.Null(CutscenePlacement.FindTargetParallel(root, root));
        Assert.Null(CutscenePlacement.FindTargetParallel(root, null));
    }

    [Fact]
    public void TheTarget_IsNullForASelectionOutsideTheTree()
    {
        var root = Parallel(Wait(1));

        Assert.Null(CutscenePlacement.FindTargetParallel(root, Parallel()));
        Assert.Null(CutscenePlacement.FindTargetParallel(root, Wait(2)));
    }

    // ---- add at a time ----

    [Fact]
    public void AddingAtATime_InsertsAnAtAtTheEndOfTheParallel_HoldingANewAction_InOneHistoryEntry()
    {
        var a = Wait(1);
        var root = Parallel(a);
        var session = Session(root);
        string before = Serialize(session);

        var command = CutscenePlacement.TryCreateAdd(session, root, CutsceneActionTypes.Wait, 2, out var created, out string error);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(command);
        history.Execute(command);

        Assert.Equal(2, root.Actions.Count);
        Assert.Same(a, root.Actions[0]);
        var at = Assert.IsType<AtCutsceneActionData>(root.Actions[1]);
        Assert.Equal(2f, at.Seconds);
        Assert.IsType<WaitCutsceneActionData>(created);
        AssertOrder(at.Actions, created!);
        Assert.Equal("Add Wait at 2 s", command.Description);

        history.Undo();
        AssertOrder(root.Actions, a);
        Assert.Equal(before, Serialize(session));
        Assert.False(history.CanUndo);

        history.Redo();
        Assert.Same(at, root.Actions[1]);
        AssertOrder(at.Actions, created!);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void AddingAtATime_CreatesTheActionWithTheDefaultsOfTheCatalogue()
    {
        var root = Parallel();
        var session = Session(root);

        var command = CutscenePlacement.TryCreateAdd(session, root, CutsceneActionTypes.WaitFrames, 0.5, out var created, out _);
        history.Execute(command!);

        var waitFrames = Assert.IsType<WaitFramesCutsceneActionData>(created);
        Assert.Equal(1, waitFrames.Frames);
        Assert.Equal(0.5f, Assert.IsType<AtCutsceneActionData>(Assert.Single(root.Actions)).Seconds);
    }

    [Fact]
    public void AnAddedTree_SavedThenReloaded_IsIdentical()
    {
        var root = Parallel(Wait(1));
        var session = Session(root);
        history.Execute(CutscenePlacement.TryCreateAdd(session, root, CutsceneActionTypes.Wait, 1.25, out _, out _)!);
        string saved = Serialize(session);

        var reloaded = CutsceneEditSession.Load(JObject.Parse(saved));

        Assert.Equal(saved, Serialize(reloaded));
        Assert.Empty(reloaded.LoadLosses);
    }

    [Fact]
    public void Adding_IsRefusedForATargetThatIsNotAParallelOfTheTree_AnUnknownTypeOrABadTime()
    {
        var x = Wait(1);
        var sequence = Sequence(x);
        var root = Parallel(sequence);
        var session = Session(root);
        string before = Serialize(session);

        Assert.Null(CutscenePlacement.TryCreateAdd(session, sequence, CutsceneActionTypes.Wait, 1, out var created, out string error));
        Assert.Null(created);
        Assert.Equal("The target is not a Parallel of the cutscene.", error);

        Assert.NotNull(CutscenePlacement.GetAddRefusal(session, x, CutsceneActionTypes.Wait, 1));
        Assert.NotNull(CutscenePlacement.GetAddRefusal(session, Parallel(), CutsceneActionTypes.Wait, 1));
        Assert.NotNull(CutscenePlacement.GetAddRefusal(session, root, "Teleport", 1));
        Assert.NotNull(CutscenePlacement.GetAddRefusal(session, root, CutsceneActionTypes.Wait, -1));
        Assert.NotNull(CutscenePlacement.GetAddRefusal(session, root, CutsceneActionTypes.Wait, double.NaN));
        Assert.Null(CutscenePlacement.GetAddRefusal(session, root, CutsceneActionTypes.Wait, 0));
        Assert.Equal("'Teleport' is not an action type of the catalogue.", CutscenePlacement.GetAddRefusal(session, root, "Teleport", 1));

        Assert.Equal(before, Serialize(session));
    }
}
