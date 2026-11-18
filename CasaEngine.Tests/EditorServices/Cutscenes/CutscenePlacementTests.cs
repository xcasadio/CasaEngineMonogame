using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutscenePlacementTests
{
    private const double Tolerance = 1e-9;

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

    private static RepeatCutsceneActionData Repeat(int? count, params CutsceneActionData[] actions)
    {
        var repeat = new RepeatCutsceneActionData { Count = count };
        repeat.Actions.AddRange(actions);
        return repeat;
    }

    private static WaitCutsceneActionData Wait(float seconds) => new() { Seconds = seconds };

    private static void AssertNone(CutsceneActionData root, CutsceneActionData action)
    {
        var rule = CutscenePlacement.Resolve(root, action);
        Assert.Equal(CutscenePlacementKind.None, rule.Kind);
        Assert.False(rule.CanDrag);
    }

    private static void AssertChangesAt(CutsceneActionData root, CutsceneActionData action, AtCutsceneActionData expected)
    {
        var rule = CutscenePlacement.Resolve(root, action);
        Assert.Equal(CutscenePlacementKind.ChangeAt, rule.Kind);
        Assert.True(rule.CanDrag);
        Assert.Same(expected, rule.At);
    }

    private static void AssertWraps(CutsceneActionData root, CutsceneActionData action, CutsceneActionData node, ParallelCutsceneActionData parallel)
    {
        var rule = CutscenePlacement.Resolve(root, action);
        Assert.Equal(CutscenePlacementKind.WrapInAt, rule.Kind);
        Assert.True(rule.CanDrag);
        Assert.Same(node, rule.Node);
        Assert.Same(parallel, rule.Parallel);
    }

    [Fact]
    public void FirstChildOfAnAt_ChangesThatAt_AndASecondChildCannotBeDragged()
    {
        var first = Wait(1);
        var second = Wait(1);
        var at = At(0.5f, first, second);
        var root = Parallel(at);

        AssertChangesAt(root, first, at);
        AssertNone(root, second);
    }

    [Fact]
    public void DirectChildOfAParallel_IsWrapped()
    {
        var wait = Wait(1);
        var other = Wait(2);
        var parallel = Parallel(wait, other);
        var root = Sequence(parallel);

        AssertWraps(root, wait, wait, parallel);
        AssertWraps(root, other, other, parallel);
    }

    [Fact]
    public void ChildOfASequenceAtTheRoot_CannotBeDragged()
    {
        var first = Wait(1);
        var second = Wait(1);
        var root = Sequence(first, second);

        AssertNone(root, first);
        AssertNone(root, second);
    }

    [Fact]
    public void ARootWithoutParent_AndAnActionOutsideTheTree_GiveNothing()
    {
        var root = Sequence(Wait(1));

        AssertNone(root, root);
        AssertNone(root, Wait(1));
    }

    [Fact]
    public void ParallelSequenceXY_XWrapsTheSequence_YGivesNothing_ThenXChangesTheAt()
    {
        var x = Wait(1);
        var y = Wait(2);
        var sequence = Sequence(x, y);
        var parallel = Parallel(sequence);
        var root = Sequence(parallel);

        AssertWraps(root, x, sequence, parallel);
        AssertNone(root, y);

        // The tree after the wrap: the At replaces the Sequence in the Parallel and holds it.
        var at = At(0, sequence);
        parallel.Actions[0] = at;

        AssertChangesAt(root, x, at);
        AssertNone(root, y);
    }

    [Fact]
    public void ParallelRepeatAtX_ChangesTheNearestAt()
    {
        var x = Wait(1);
        var at = At(0.5f, x);
        var repeat = Repeat(2, at);
        var root = Parallel(repeat);

        AssertChangesAt(root, x, at);
    }

    [Fact]
    public void ParallelAtSequenceX_ChangesTheAt()
    {
        var x = Wait(1);
        var y = Wait(1);
        var at = At(0.5f, Sequence(x, y));
        var root = Parallel(at);

        AssertChangesAt(root, x, at);
        AssertNone(root, y);
    }

    [Fact]
    public void NestedAtActions_TheNearestOneChanges()
    {
        var x = Wait(1);
        var inner = At(0.5f, x);
        var outer = At(2, inner);
        var root = Parallel(outer);

        AssertChangesAt(root, x, inner);
        AssertChangesAt(root, inner, outer);
    }

    [Fact]
    public void AnAtThatIsNotTheFirstChildOfASequence_StopsTheRule()
    {
        var x = Wait(1);
        var at = At(0.5f, x);
        var root = Parallel(Sequence(Wait(1), at));

        AssertChangesAt(root, x, at);
        AssertNone(root, at);
    }

    [Fact]
    public void ARepeatOfThreeLaps_TheLapRangeChangesTheAt_TheMoreRangeCannotBeDragged()
    {
        var x = Wait(1);
        var repeat = Repeat(3, x);
        var at = At(0.5f, repeat);
        var root = Parallel(at);

        var layout = CutsceneTimelineProjection.Build(root);
        var lap = layout.Items.Single(item => item.Role == CutsceneTimelineItemRole.RepeatLap);
        var more = layout.Items.Single(item => item.Role == CutsceneTimelineItemRole.RepeatMoreLaps);

        Assert.True(lap.IsPrimary);
        Assert.False(more.IsPrimary);

        var lapRule = CutscenePlacement.Resolve(root, lap);
        Assert.Equal(CutscenePlacementKind.ChangeAt, lapRule.Kind);
        Assert.Same(at, lapRule.At);

        var moreRule = CutscenePlacement.Resolve(root, more);
        Assert.Equal(CutscenePlacementKind.None, moreRule.Kind);
    }

    [Fact]
    public void EveryItemOtherThanTheMoreLapsRange_IsPrimary()
    {
        var root = Parallel(Wait(1), Repeat(2, Wait(1)), At(1, Wait(1)));
        var layout = CutsceneTimelineProjection.Build(root);

        foreach (var item in layout.Items)
        {
            Assert.Equal(item.Role != CutsceneTimelineItemRole.RepeatMoreLaps, item.IsPrimary);
        }
    }

    [Fact]
    public void TheRuleOfAnItem_IsTheRuleOfItsAction()
    {
        var wait = Wait(1);
        var parallel = Parallel(wait);
        var root = Sequence(parallel);
        var layout = CutsceneTimelineProjection.Build(root);
        var item = layout.Items.Single();

        var rule = CutscenePlacement.Resolve(root, item);

        Assert.Equal(CutscenePlacementKind.WrapInAt, rule.Kind);
        Assert.Same(wait, rule.Node);
        Assert.Same(parallel, rule.Parallel);
    }

    [Fact]
    public void ConvertDrop_AddsTheMoveToTheCurrentSeconds()
    {
        // An At of 0.5 s whose child starts at 2.5: dropped 0.7 s later, it is at 1.2 s.
        Assert.Equal(1.2, CutscenePlacement.ConvertDrop(0.5, 2.5, 3.2), 1e-9);
    }

    [Fact]
    public void ConvertDrop_ForAWrap_StartsFromZero()
    {
        // A child of a Parallel that starts at 1.0 and is dropped at 2.5 is wrapped in an At of 1.5 s.
        Assert.Equal(1.5, CutscenePlacement.ConvertDrop(0, 1.0, 2.5), 1e-9);
    }

    [Fact]
    public void ConvertDrop_EarlierThanZero_IsClampedToZero()
    {
        Assert.Equal(0, CutscenePlacement.ConvertDrop(0.5, 2.5, 1.0), Tolerance);
        Assert.Equal(0, CutscenePlacement.ConvertDrop(0, 1.0, 0.2), Tolerance);
    }

    [Fact]
    public void ConvertDrop_BetweenTwoFrames_IsAlignedOnTheNearestFrame()
    {
        // 0.5 + 0.7 + 0.004 = 1.204: 72.24 frames, so 72 frames.
        Assert.Equal(72 / 60.0, CutscenePlacement.ConvertDrop(0.5, 2.5, 3.204), 1e-9);
        // 0.5 + 0.7 + 0.01 = 1.21: 72.6 frames, so 73 frames.
        Assert.Equal(73 / 60.0, CutscenePlacement.ConvertDrop(0.5, 2.5, 3.21), 1e-9);
    }

    [Fact]
    public void ConvertDrop_WithoutMove_KeepsTheSeconds()
    {
        Assert.Equal(0.5, CutscenePlacement.ConvertDrop(0.5, 2.5, 2.5), 1e-9);
    }

    [Fact]
    public void ConvertDrop_WithAValueThatIsNotANumber_KeepsTheSeconds()
    {
        Assert.Equal(0.5, CutscenePlacement.ConvertDrop(0.5, 2.5, double.NaN), 1e-9);
        Assert.Equal(0.5, CutscenePlacement.ConvertDrop(0.5, double.PositiveInfinity, 3), 1e-9);
        Assert.Equal(0.5, CutscenePlacement.ConvertDrop(0.5, 2.5, double.NegativeInfinity), 1e-9);
    }

    [Fact]
    public void LowestInstant_IsTheTimeWhereTheSecondsWouldBeZero()
    {
        Assert.Equal(2.0, CutscenePlacement.LowestInstant(0.5, 2.5), Tolerance);
        Assert.Equal(1.0, CutscenePlacement.LowestInstant(0, 1.0), Tolerance);
        Assert.Equal(0, CutscenePlacement.LowestInstant(3, 1.0), Tolerance);
    }

    [Fact]
    public void ConvertDropAtTheLowestInstant_GivesZeroSeconds()
    {
        double lowest = CutscenePlacement.LowestInstant(0.5, 2.5);

        Assert.Equal(0, CutscenePlacement.ConvertDrop(0.5, 2.5, lowest), Tolerance);
    }

    [Fact]
    public void AParallelPlacedAfterAOneSecondWait_StartsAtOne()
    {
        var parallel = Parallel(Wait(1));
        var root = Sequence(Wait(1), parallel);
        var layout = CutsceneTimelineProjection.Build(root);

        Assert.True(layout.TryGetContainerStart(parallel, out double start));
        Assert.Equal(1.0, start, Tolerance);
        Assert.True(layout.TryGetContainerStart(root, out double rootStart));
        Assert.Equal(0, rootStart, Tolerance);
    }

    [Fact]
    public void AnEmptyParallel_HasAStart()
    {
        var parallel = new ParallelCutsceneActionData();
        var root = Sequence(Wait(2), parallel);
        var layout = CutsceneTimelineProjection.Build(root);

        Assert.True(layout.TryGetContainerStart(parallel, out double start));
        Assert.Equal(2.0, start, Tolerance);
    }

    [Fact]
    public void AnEmptySequenceRepeatAndAt_EachHaveAStart()
    {
        var sequence = new SequenceCutsceneActionData();
        var repeat = new RepeatCutsceneActionData { Count = 2 };
        var at = new AtCutsceneActionData { Seconds = 1 };
        var root = Sequence(Wait(1), sequence, Wait(1), repeat, Wait(1), at);
        var layout = CutsceneTimelineProjection.Build(root);

        Assert.True(layout.TryGetContainerStart(sequence, out double sequenceStart));
        Assert.Equal(1.0, sequenceStart, Tolerance);
        Assert.True(layout.TryGetContainerStart(repeat, out double repeatStart));
        Assert.Equal(2.0, repeatStart, Tolerance);
        Assert.True(layout.TryGetContainerStart(at, out double atStart));
        Assert.Equal(3.0, atStart, Tolerance);
    }

    [Fact]
    public void AParallelInARepeat_StartsInTheDrawnLap()
    {
        var parallel = Parallel(Wait(1));
        var repeat = Repeat(3, Wait(0.5f), parallel);
        var root = Sequence(Wait(2), repeat);
        var layout = CutsceneTimelineProjection.Build(root);

        Assert.True(layout.TryGetContainerStart(parallel, out double start));
        Assert.Equal(2.5, start, Tolerance);
        Assert.True(layout.TryGetContainerStart(repeat, out double repeatStart));
        Assert.Equal(2.0, repeatStart, Tolerance);
    }

    [Fact]
    public void TheChildrenOfAnAt_AreInsideItsDelay_ButTheAtStartsBeforeIt()
    {
        var inner = Parallel(Wait(1));
        var at = At(1.5f, inner);
        var root = Parallel(at);
        var layout = CutsceneTimelineProjection.Build(root);

        Assert.True(layout.TryGetContainerStart(at, out double atStart));
        Assert.Equal(0, atStart, Tolerance);
        Assert.True(layout.TryGetContainerStart(inner, out double innerStart));
        Assert.Equal(1.5, innerStart, Tolerance);
    }

    [Fact]
    public void ANonContainer_AndAnActionOutsideTheLayout_HaveNoContainerStart()
    {
        var wait = Wait(1);
        var root = Sequence(wait);
        var layout = CutsceneTimelineProjection.Build(root);

        Assert.False(layout.TryGetContainerStart(wait, out _));
        Assert.False(layout.TryGetContainerStart(Parallel(), out _));
        Assert.False(CutsceneTimelineLayout.Empty.TryGetContainerStart(root, out _));
    }
}
