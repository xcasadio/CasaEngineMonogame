using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutscenePlayTimingsTests
{
    private static CutsceneActionData LoadLoop()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var asset = new CutsceneAsset();
        asset.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory.FullName, "Projects", "SpriteCutsceneSample", "Cutscenes", "loop.cutscene"))));
        return asset.RootAction!;
    }

    private static CutsceneActionNotification At(CutsceneActionData action, double seconds) => new(action, seconds, (long)(seconds * 10));

    [Fact]
    public void MapByShape_MapsEveryActionOfASerializedCopyToItsOriginal()
    {
        var original = LoadLoop();
        var copy = CutsceneActionCatalog.Clone(original);

        var map = CutsceneActionTree.MapByShape(copy, original);

        Assert.NotNull(map);
        Assert.Equal(CutsceneActionTree.CountActions(original), map.Count);
        Assert.Same(original, map[copy]);
        var copyChildren = CutsceneActionCatalog.GetChildren(copy);
        var originalChildren = CutsceneActionCatalog.GetChildren(original);
        for (int index = 0; index < copyChildren.Count; index++)
        {
            Assert.NotSame(originalChildren[index], copyChildren[index]);
            Assert.Same(originalChildren[index], map[copyChildren[index]]);
        }

        var copyRepeat = (RepeatCutsceneActionData)copyChildren[1];
        var originalRepeat = (RepeatCutsceneActionData)originalChildren[1];
        for (int index = 0; index < copyRepeat.Actions.Count; index++)
        {
            Assert.Same(originalRepeat.Actions[index], map[copyRepeat.Actions[index]]);
        }
    }

    [Fact]
    public void MapByShape_RefusesTreesOfDifferentShapes()
    {
        var original = LoadLoop();

        var otherType = CutsceneActionCatalog.Clone(original);
        ((RepeatCutsceneActionData)CutsceneActionCatalog.GetChildren(otherType)[1]).Actions[0] = new WaitCutsceneActionData();
        Assert.Null(CutsceneActionTree.MapByShape(otherType, original));

        var otherCount = CutsceneActionCatalog.Clone(original);
        ((SequenceCutsceneActionData)otherCount).Actions.RemoveAt(0);
        Assert.Null(CutsceneActionTree.MapByShape(otherCount, original));

        Assert.Null(CutsceneActionTree.MapByShape(new WaitCutsceneActionData(), new SequenceCutsceneActionData()));
    }

    private static (CutscenePlayTimings Timings, WaitCutsceneActionData Original, WaitCutsceneActionData Copy) StartedWait()
    {
        var original = new WaitCutsceneActionData { Seconds = 1 };
        var copy = new WaitCutsceneActionData { Seconds = 1 };
        var timings = new CutscenePlayTimings();
        timings.Begin(new Dictionary<CutsceneActionData, CutsceneActionData> { [copy] = original });
        return (timings, original, copy);
    }

    [Fact]
    public void RecordsTheDurationOfAnActionAsEndMinusStart()
    {
        var (timings, original, copy) = StartedWait();
        int version = timings.Version;

        timings.OnStarted(At(copy, 0.5));
        timings.OnEnded(At(copy, 1.6));

        Assert.True(timings.TryGetDuration(original, out double seconds));
        Assert.Equal(1.1, seconds, 1e-9);
        Assert.Equal(1.1, timings.Durations[original], 1e-9);

        // A Wait shows its own length, not a measured one: the projection does not change.
        Assert.Equal(version, timings.Version);
    }

    [Fact]
    public void TheVersionChanges_OnlyWhenAMeasureUsedByTheProjectionIsRecorded()
    {
        var wait = new WaitCutsceneActionData { Seconds = 1 };
        var waitCopy = new WaitCutsceneActionData { Seconds = 1 };
        var moveTo = new MoveToCutsceneActionData { EntityName = "a" };
        var moveToCopy = new MoveToCutsceneActionData { EntityName = "a" };
        var timings = new CutscenePlayTimings();
        timings.Begin(new Dictionary<CutsceneActionData, CutsceneActionData> { [waitCopy] = wait, [moveToCopy] = moveTo });
        int version = timings.Version;

        timings.OnStarted(At(waitCopy, 0));
        timings.OnEnded(At(waitCopy, 1));
        Assert.Equal(version, timings.Version);

        timings.OnStarted(At(moveToCopy, 1));
        timings.OnEnded(At(moveToCopy, 2.5));
        Assert.NotEqual(version, timings.Version);
        Assert.Equal(1.5, timings.Durations[moveTo], 1e-9);
    }

    [Fact]
    public void KeepsTheFirstOccurrenceOnly()
    {
        var (timings, original, copy) = StartedWait();

        timings.OnStarted(At(copy, 0));
        timings.OnEnded(At(copy, 1));
        timings.OnStarted(At(copy, 5));
        timings.OnEnded(At(copy, 5.2));

        Assert.Equal(1, timings.Durations[original], 1e-9);
    }

    [Fact]
    public void IgnoresActionsOutsideTheMap_AndAnEndWithoutAStart()
    {
        var (timings, original, copy) = StartedWait();

        timings.OnStarted(At(new WaitCutsceneActionData(), 0));
        timings.OnEnded(At(new WaitCutsceneActionData(), 1));
        timings.OnEnded(At(copy, 1));

        Assert.Empty(timings.Durations);
        Assert.False(timings.TryGetDuration(original, out _));
    }

    [Fact]
    public void AnEffectActionIsNotMeasuredByItsNotifications_ButByTheEffectDuration()
    {
        var original = new MoveSpriteCutsceneActionData { EntityName = "a" };
        var copy = new MoveSpriteCutsceneActionData { EntityName = "a" };
        var timings = new CutscenePlayTimings();
        timings.Begin(new Dictionary<CutsceneActionData, CutsceneActionData> { [copy] = original });

        timings.OnStarted(At(copy, 0));
        timings.OnEnded(At(copy, 0));
        Assert.Empty(timings.Durations);

        timings.SetEffectDuration(original, 1.083);
        timings.SetEffectDuration(original, 9);

        Assert.Equal(1.083, timings.Durations[original], 1e-9);
    }

    [Fact]
    public void Clear_ForgetsTheMeasures_ButKeepsTheMapOfThePlayInProgress()
    {
        var (timings, original, copy) = StartedWait();
        timings.OnStarted(At(copy, 0));
        timings.OnEnded(At(copy, 1));
        int version = timings.Version;

        timings.Clear();

        Assert.Empty(timings.Durations);
        Assert.NotEqual(version, timings.Version);

        timings.OnStarted(At(copy, 2));
        timings.OnEnded(At(copy, 4));
        Assert.Equal(2, timings.Durations[original], 1e-9);
    }

    [Fact]
    public void Begin_StartsANewRecord()
    {
        var (timings, original, copy) = StartedWait();
        timings.OnStarted(At(copy, 0));
        timings.OnEnded(At(copy, 1));

        var secondCopy = new WaitCutsceneActionData();
        timings.Begin(new Dictionary<CutsceneActionData, CutsceneActionData> { [secondCopy] = original });

        Assert.Empty(timings.Durations);
        timings.OnStarted(At(copy, 0));
        Assert.Empty(timings.Durations);
    }

    [Fact]
    public void RecordingDoesNotAllocate_OnceThePlayHasBegun()
    {
        var original = LoadLoop();
        var copy = CutsceneActionCatalog.Clone(original);
        var map = CutsceneActionTree.MapByShape(copy, original)!;
        var timings = new CutscenePlayTimings();
        timings.Begin(map);
        var actions = map.Keys.ToArray();

        // Warm up the code paths, then measure.
        timings.OnStarted(At(actions[0], 0));
        timings.OnEnded(At(actions[0], 0.1));
        timings.Clear();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < actions.Length; index++)
        {
            var notification = At(actions[index], index);
            timings.OnStarted(in notification);
            var ended = At(actions[index], index + 0.5);
            timings.OnEnded(in ended);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
