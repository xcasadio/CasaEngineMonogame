using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneTimelineProjectionTests
{
    private const double Tolerance = 1e-3;

    private static CutsceneAsset LoadSample(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var asset = new CutsceneAsset();
        asset.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory.FullName, "Projects", "SpriteCutsceneSample", "Cutscenes", file))));
        return asset;
    }

    private static CutsceneTimelineItem[] ItemsOf(CutsceneTimelineLayout layout, string type)
        => layout.Items.Where(item => item.Action.Type == type && item.Role == CutsceneTimelineItemRole.Action).ToArray();

    private static SequenceCutsceneActionData Sequence(params CutsceneActionData[] actions)
    {
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.AddRange(actions);
        return sequence;
    }

    private static string[] RowLabels(CutsceneTimelineLayout layout) => layout.Rows.Select(row => row.Label).ToArray();

    private static void AssertTimes(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], actual[index], Tolerance);
        }
    }

    [Fact]
    public void Loop_HasTheExpectedRowsAndTimes()
    {
        var layout = CutsceneTimelineProjection.Build(LoadSample("loop.cutscene").RootAction!);

        Assert.Equal(["Loops", "Flow", "orb-left", "orb-right", "ring-left", "ring-right", "World and input"], RowLabels(layout));
        Assert.Equal(5.6, layout.DurationSeconds, Tolerance);
        Assert.Equal(0, layout.LoopStartSeconds!.Value, Tolerance);
        Assert.Equal(5.6, layout.LoopEndSeconds!.Value, Tolerance);

        var waits = ItemsOf(layout, CutsceneActionTypes.Wait);
        AssertTimes([0.0, 1.3, 3.8, 5.0], waits.Select(item => item.StartSeconds).ToArray());
        AssertTimes([1.3, 2.5, 1.2, 0.6], waits.Select(item => item.DurationSeconds).ToArray());
        Assert.All(waits, item => Assert.Equal(1, item.RowIndex));

        var tints = ItemsOf(layout, CutsceneActionTypes.TintSprite);
        Assert.Equal(8, tints.Length);
        Assert.Equal(1.3, tints[0].StartSeconds, Tolerance);
        Assert.Equal(0.8, tints[0].DurationSeconds, Tolerance);
        Assert.Equal(3.8, tints[4].StartSeconds, Tolerance);
        Assert.Equal(1.0, tints[4].DurationSeconds, Tolerance);

        var hides = ItemsOf(layout, CutsceneActionTypes.HideSprite);
        Assert.Equal(4, hides.Length);
        Assert.All(hides, item =>
        {
            Assert.Equal(5.0, item.StartSeconds, Tolerance);
            Assert.Equal(CutsceneTimelineItemShape.Marker, item.Shape);
        });

        var moves = ItemsOf(layout, CutsceneActionTypes.MoveSprite);
        Assert.All(moves, item =>
        {
            Assert.Equal(0, item.StartSeconds, Tolerance);
            Assert.Equal(1.0, item.DurationSeconds, Tolerance);
            Assert.True(item.IsEstimated);
            Assert.False(item.IsMeasured);
        });

        var range = Assert.Single(layout.Items, item => item.Role == CutsceneTimelineItemRole.RepeatLap);
        Assert.Equal("Repeat forever", range.Label);
        Assert.Equal(0, range.RowIndex);
        Assert.Equal(5.6, range.DurationSeconds, Tolerance);

        var exit = Assert.Single(ItemsOf(layout, CutsceneActionTypes.ExitOnInput));
        Assert.Equal(CutsceneTimelineItemShape.OpenBar, exit.Shape);
        Assert.Equal(6, exit.RowIndex);
        Assert.Equal(5.6, exit.DurationSeconds, Tolerance);

        // Ranges, then bars, then markers.
        int[] order = layout.Items
            .Select(item => item.Shape == CutsceneTimelineItemShape.Range ? 0 : item.Shape == CutsceneTimelineItemShape.Marker ? 2 : 1)
            .ToArray();
        Assert.Equal(order.OrderBy(value => value).ToArray(), order);
    }

    [Fact]
    public void Exit_HasAFadeAndAChangeWorld()
    {
        var layout = CutsceneTimelineProjection.Build(LoadSample("exit.cutscene").RootAction!);

        Assert.Equal(["Screen", "World and input"], RowLabels(layout));
        var fade = Assert.Single(ItemsOf(layout, CutsceneActionTypes.FadeScreen));
        Assert.Equal(0, fade.StartSeconds, Tolerance);
        Assert.Equal(1, fade.DurationSeconds, Tolerance);
        Assert.Equal(CutsceneTimelineItemShape.Bar, fade.Shape);
        var change = Assert.Single(ItemsOf(layout, CutsceneActionTypes.ChangeWorld));
        Assert.Equal(1, change.StartSeconds, Tolerance);
        Assert.Equal(CutsceneTimelineItemShape.Marker, change.Shape);
        Assert.Null(layout.LoopStartSeconds);
        Assert.Equal(1, layout.DurationSeconds, Tolerance);
    }

    [Fact]
    public void EveryActionTypeHasAnItemOrIsExplicitlyWithoutOne()
    {
        foreach (var kind in CutsceneActionCatalog.Kinds)
        {
            var action = CutsceneActionCatalog.Create(kind.Type);
            var layout = CutsceneTimelineProjection.Build(Sequence(action));

            if (CutsceneTimelineProjection.HasElement(kind.Type))
            {
                Assert.True(layout.Items.Any(item => ReferenceEquals(item.Action, action)), kind.Type);
                Assert.NotEmpty(layout.Rows);
            }
            else
            {
                Assert.Empty(layout.Items);
                Assert.Contains(kind.Type, new[] { CutsceneActionTypes.Sequence, CutsceneActionTypes.Parallel, CutsceneActionTypes.At });
            }
        }
    }

    [Fact]
    public void UnknownType_IsAMarkerOnTheOtherRow()
    {
        var layout = CutsceneTimelineProjection.Build(Sequence(new UnknownCutsceneActionData("Mystery")));

        Assert.Equal(["Other"], RowLabels(layout));
        Assert.Equal(CutsceneTimelineItemShape.Marker, Assert.Single(layout.Items).Shape);
    }

    [Fact]
    public void Parallel_InsideARepeat_StartsBranchesTogetherAndLastsAsLongAsTheLongest()
    {
        var parallel = new ParallelCutsceneActionData();
        parallel.Actions.Add(new WaitCutsceneActionData { Seconds = 1 });
        parallel.Actions.Add(Sequence(new WaitCutsceneActionData { Seconds = 2 }, new WaitCutsceneActionData { Seconds = 1 }));
        var repeat = new RepeatCutsceneActionData { Count = 2 };
        repeat.Actions.Add(parallel);
        var after = new HideSpriteCutsceneActionData { EntityName = "a" };

        var layout = CutsceneTimelineProjection.Build(Sequence(repeat, after));

        var waits = ItemsOf(layout, CutsceneActionTypes.Wait);
        AssertTimes([0.0, 0.0, 2.0], waits.Select(item => item.StartSeconds).ToArray());

        // The two bars that start together overlap, so they take two sub-rows of the Flow row.
        int flow = waits[0].RowIndex;
        Assert.Equal("Flow", layout.Rows[flow].Label);
        Assert.Equal(2, layout.Rows[flow].SubRowCount);
        Assert.NotEqual(waits[0].SubRow, waits[1].SubRow);

        // One lap of 3 s, drawn once and run twice: the action after the repeat starts at 6 s.
        Assert.Equal(6, Assert.Single(ItemsOf(layout, CutsceneActionTypes.HideSprite)).StartSeconds, Tolerance);
    }

    [Fact]
    public void Repeat_OfThreeLaps_DrawsOneLapAndAMoreRange()
    {
        var repeat = new RepeatCutsceneActionData { Count = 3 };
        repeat.Actions.Add(new WaitCutsceneActionData { Seconds = 2 });
        var after = new WaitCutsceneActionData { Seconds = 1 };

        var layout = CutsceneTimelineProjection.Build(Sequence(repeat, after));

        var lap = Assert.Single(layout.Items, item => item.Role == CutsceneTimelineItemRole.RepeatLap);
        var more = Assert.Single(layout.Items, item => item.Role == CutsceneTimelineItemRole.RepeatMoreLaps);
        Assert.Equal("Repeat ×3", lap.Label);
        Assert.Equal(0, lap.StartSeconds, Tolerance);
        Assert.Equal(2, lap.DurationSeconds, Tolerance);
        Assert.Equal("×2 more", more.Label);
        Assert.Equal(2, more.StartSeconds, Tolerance);
        Assert.Equal(4, more.DurationSeconds, Tolerance);
        Assert.Same(repeat, more.Action);
        Assert.Equal(6, layout.Items.Single(item => ReferenceEquals(item.Action, after)).StartSeconds, Tolerance);
        Assert.Equal(7, layout.DurationSeconds, Tolerance);
        Assert.Null(layout.LoopStartSeconds);
    }

    [Fact]
    public void OverlappingBarsOfOneEntity_GoToSeparateSubRows_AndMarkersStayOnTheFirst()
    {
        var first = new TintSpriteCutsceneActionData { EntityName = "a", Duration = 2 };
        var second = new TintSpriteCutsceneActionData { EntityName = "a", Duration = 1 };
        var marker = new HideSpriteCutsceneActionData { EntityName = "a" };

        var layout = CutsceneTimelineProjection.Build(Sequence(first, second, marker));

        var row = Assert.Single(layout.Rows);
        Assert.Equal(2, row.SubRowCount);
        Assert.Equal(0, layout.Items.Single(item => ReferenceEquals(item.Action, first)).SubRow);
        Assert.Equal(1, layout.Items.Single(item => ReferenceEquals(item.Action, second)).SubRow);
        Assert.Equal(0, layout.Items.Single(item => ReferenceEquals(item.Action, marker)).SubRow);
    }

    [Fact]
    public void UnknownDuration_IsEstimatedWithoutAMeasure_AndMeasuredWithOne()
    {
        var moveTo = new MoveToCutsceneActionData { EntityName = "a" };
        var after = new HideSpriteCutsceneActionData { EntityName = "a" };
        var root = Sequence(moveTo, after);

        var estimated = CutsceneTimelineProjection.Build(root);
        var bar = estimated.Items.Single(item => ReferenceEquals(item.Action, moveTo));
        Assert.Equal(1, bar.DurationSeconds, Tolerance);
        Assert.True(bar.IsEstimated);
        Assert.False(bar.IsMeasured);
        Assert.Equal(1, estimated.Items.Single(item => ReferenceEquals(item.Action, after)).StartSeconds, Tolerance);

        var measured = CutsceneTimelineProjection.Build(root, new Dictionary<CutsceneActionData, double> { [moveTo] = 2.5 });
        bar = measured.Items.Single(item => ReferenceEquals(item.Action, moveTo));
        Assert.Equal(2.5, bar.DurationSeconds, Tolerance);
        Assert.False(bar.IsEstimated);
        Assert.True(bar.IsMeasured);
        Assert.Equal(2.5, measured.Items.Single(item => ReferenceEquals(item.Action, after)).StartSeconds, Tolerance);
    }

    [Fact]
    public void MoveSprite_DoesNotHoldTheSequence()
    {
        var move = new MoveSpriteCutsceneActionData { EntityName = "a", X = 1, Y = 1, Speed = 1 };
        var after = new HideSpriteCutsceneActionData { EntityName = "a" };

        var layout = CutsceneTimelineProjection.Build(Sequence(move, after), new Dictionary<CutsceneActionData, double> { [move] = 1.1 });

        var bar = layout.Items.Single(item => ReferenceEquals(item.Action, move));
        Assert.Equal(1.1, bar.DurationSeconds, Tolerance);
        Assert.True(bar.IsMeasured);
        Assert.Equal(0, layout.Items.Single(item => ReferenceEquals(item.Action, after)).StartSeconds, Tolerance);
    }

    [Fact]
    public void Frames_AreConvertedAtSixtyPerSecond_AndEstimated()
    {
        var wait = new WaitFramesCutsceneActionData { Frames = 30 };
        var fade = new FadeScreenFramesCutsceneActionData { Frames = 60 };

        var layout = CutsceneTimelineProjection.Build(Sequence(wait, fade));

        var waitItem = layout.Items.Single(item => ReferenceEquals(item.Action, wait));
        Assert.Equal(0.5, waitItem.DurationSeconds, Tolerance);
        Assert.True(waitItem.IsEstimated);
        var fadeItem = layout.Items.Single(item => ReferenceEquals(item.Action, fade));
        Assert.Equal(0.5, fadeItem.StartSeconds, Tolerance);
        Assert.Equal(1, fadeItem.DurationSeconds, Tolerance);
        Assert.True(fadeItem.IsEstimated);
    }

    [Fact]
    public void MapPlayTime_WrapsOnTheDrawnLap_AfterTheLoopStarts()
    {
        // loop.cutscene starts with an ExitOnInput (no duration) before its endless Repeat.
        var layout = CutsceneTimelineProjection.Build(LoadSample("loop.cutscene").RootAction!);

        Assert.Equal(0, layout.MapPlayTime(0), Tolerance);
        Assert.Equal(3, layout.MapPlayTime(3), Tolerance);
        Assert.Equal(2.4, layout.MapPlayTime(8), Tolerance);
        Assert.Equal(0.8, layout.MapPlayTime(12), Tolerance);

        // A wait before the loop: times before the loop are kept, then it wraps from the loop start.
        var loop = new RepeatCutsceneActionData { Count = 0 };
        loop.Actions.Add(new WaitCutsceneActionData { Seconds = 2 });
        var shifted = CutsceneTimelineProjection.Build(Sequence(new WaitCutsceneActionData { Seconds = 1 }, loop));
        Assert.Equal(0.5, shifted.MapPlayTime(0.5), Tolerance);
        Assert.Equal(1.0, shifted.MapPlayTime(1.0), Tolerance);
        Assert.Equal(2.0, shifted.MapPlayTime(2.0), Tolerance);
        Assert.Equal(1.5, shifted.MapPlayTime(5.5), Tolerance);
    }

    [Fact]
    public void MapPlayTime_IsTheIdentity_WithoutAnEndlessLoopOrWithAnEmptyLap()
    {
        Assert.Equal(7, CutsceneTimelineProjection.Build(Sequence(new WaitCutsceneActionData { Seconds = 1 })).MapPlayTime(7));

        var emptyLoop = new RepeatCutsceneActionData { Count = 0 };
        Assert.Equal(7, CutsceneTimelineProjection.Build(Sequence(emptyLoop)).MapPlayTime(7));
    }

    [Fact]
    public void EmptyTree_AndNonContainerRoot()
    {
        Assert.Empty(CutsceneTimelineProjection.Build(null).Items);

        var empty = CutsceneTimelineProjection.Build(new SequenceCutsceneActionData());
        Assert.Empty(empty.Rows);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.DurationSeconds);

        var single = CutsceneTimelineProjection.Build(new WaitCutsceneActionData { Seconds = 2 });
        Assert.Equal(["Flow"], RowLabels(single));
        Assert.Equal(2, single.DurationSeconds, Tolerance);
    }

    [Fact]
    public void Build_DoesNotModifyTheTree()
    {
        var asset = LoadSample("loop.cutscene");
        var before = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, before);

        CutsceneTimelineProjection.Build(asset.RootAction);

        var after = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, after);
        Assert.True(JToken.DeepEquals(before, after));
    }

    [Fact]
    public void EntityWithoutAName_GetsTheNoEntityRow()
    {
        var layout = CutsceneTimelineProjection.Build(Sequence(new ShowSpriteCutsceneActionData { EntityName = "" }));
        Assert.Equal(["(no entity)"], RowLabels(layout));
    }
}
