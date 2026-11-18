using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

/// <summary>What the editor services know about the At action: catalogue, properties, summary and timeline projection.</summary>
public sealed class AtCutsceneActionEditingTests
{
    private const double Tolerance = 1e-6;

    // ---- Catalogue, properties, summary ----

    [Fact]
    public void TheCatalogue_CreatesAnAtOfZeroSecondsInTheFlowCategory()
    {
        var action = CutsceneActionCatalog.Create(CutsceneActionTypes.At);

        var at = Assert.IsType<AtCutsceneActionData>(action);
        Assert.Equal(0f, at.Seconds);
        Assert.Empty(at.Actions);
        Assert.Equal(CutsceneActionCatalog.FlowCategory, CutsceneActionCatalog.Kinds.Single(kind => kind.Type == CutsceneActionTypes.At).Category);
        Assert.True(CutsceneActionCatalog.CanHaveChildren(at));
    }

    [Fact]
    public void TheCatalogue_ClonesAnAtWithItsChildren()
    {
        var at = new AtCutsceneActionData { Seconds = 1.5f };
        at.Actions.Add(new MoveSpriteCutsceneActionData { EntityName = "orb", X = 1, Y = 2, Speed = 3 });
        at.Actions.Add(new WaitCutsceneActionData { Seconds = 0.5f });

        var clone = Assert.IsType<AtCutsceneActionData>(CutsceneActionCatalog.Clone(at));

        Assert.NotSame(at, clone);
        Assert.NotSame(at.Actions, clone.Actions);
        Assert.Equal(1.5f, clone.Seconds);
        Assert.Equal(2, clone.Actions.Count);
        Assert.NotSame(at.Actions[0], clone.Actions[0]);
        Assert.Equal(Describe(at), Describe(clone));
    }

    [Fact]
    public void ThePropertiesOfAnAt_ReadAndWriteSeconds()
    {
        var at = new AtCutsceneActionData();
        var property = CutsceneActionPropertyEditor.FindProperty(at, "seconds")!;

        property.SetValue(at, 2.5f);

        Assert.Equal(CutsceneActionPropertyKind.Number, property.Kind);
        Assert.Equal(0d, property.Minimum);
        Assert.Equal(2.5f, at.Seconds);
        Assert.Equal(2.5f, property.GetValue(at));
        Assert.Single(CutsceneActionPropertyEditor.GetProperties(at));
        Assert.False(CutsceneActionPropertyEditor.IsExempt(at));
    }

    [Fact]
    public void TheFormatErrorsOfAnAt_AreReachable()
    {
        var session = CutsceneEditSession.Load(Root(JObject.Parse("{\"type\":\"At\",\"seconds\":\"x\",\"actions\":[]}")));
        var at = Assert.IsType<AtCutsceneActionData>(session.Asset.RootAction);

        var errors = CutsceneActionPropertyEditor.GetFormatErrors(at);

        Assert.Same(at.FormatErrors, errors);
        Assert.True(errors!.ContainsKey("seconds"));
    }

    [Fact]
    public void TheSummaryOfAnAt_FollowsTheFormOfWait()
    {
        Assert.Equal("At  1.5 s", CutsceneActionSummary.Describe(new AtCutsceneActionData { Seconds = 1.5f }));
        Assert.Equal("Wait  1.5 s", CutsceneActionSummary.Describe(new WaitCutsceneActionData { Seconds = 1.5f }));
    }

    [Fact]
    public void TheLoadFidelity_FindsTheChildrenOfAnAt_AndNoLossForAFileThatUsesIt()
    {
        var source = Root(JObject.Parse("{\"type\":\"At\",\"seconds\":1.5,\"actions\":[{\"type\":\"Wait\",\"seconds\":0.5}]}"));

        var session = CutsceneEditSession.Load(source);

        Assert.Empty(session.LoadLosses);
        var at = Assert.IsType<AtCutsceneActionData>(session.Asset.RootAction);
        Assert.Equal(0.5f, Assert.IsType<WaitCutsceneActionData>(Assert.Single(at.Actions)).Seconds);
    }

    // ---- Projection ----

    [Fact]
    public void HasElement_IsFalseForAt()
    {
        Assert.False(CutsceneTimelineProjection.HasElement("At"));
        Assert.True(CutsceneTimelineProjection.HasElement("Wait"));
    }

    [Fact]
    public void AParallelOfThreeAts_PlacesTheirChildrenAtTheirTimes_AndLastsAsLongAsTheLongest()
    {
        var parallel = new ParallelCutsceneActionData();
        var waits = new[] { new WaitCutsceneActionData { Seconds = 1f }, new WaitCutsceneActionData { Seconds = 1f }, new WaitCutsceneActionData { Seconds = 2f } };
        float[] times = [0f, 0.5f, 1.5f];
        for (int index = 0; index < 3; index++)
        {
            var at = new AtCutsceneActionData { Seconds = times[index] };
            at.Actions.Add(waits[index]);
            parallel.Actions.Add(at);
        }

        var layout = CutsceneTimelineProjection.Build(parallel);

        Assert.Equal(3, layout.Items.Count);
        Assert.DoesNotContain(layout.Items, item => item.Action is AtCutsceneActionData);
        for (int index = 0; index < 3; index++)
        {
            var item = Assert.Single(layout.Items, candidate => ReferenceEquals(candidate.Action, waits[index]));
            Assert.Equal(times[index], item.StartSeconds, Tolerance);
        }

        Assert.Equal(3.5, layout.DurationSeconds, Tolerance);
    }

    [Fact]
    public void AnAtInASequence_ShiftsWhatFollowsByItsSecondsPlusTheDurationOfItsChildren()
    {
        var inside = new WaitCutsceneActionData { Seconds = 0.75f };
        var at = new AtCutsceneActionData { Seconds = 1.5f };
        at.Actions.Add(inside);
        var after = new WaitCutsceneActionData { Seconds = 1f };
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.Add(new WaitCutsceneActionData { Seconds = 0.25f });
        sequence.Actions.Add(at);
        sequence.Actions.Add(after);

        var layout = CutsceneTimelineProjection.Build(sequence);

        Assert.Equal(1.75, Start(layout, inside), Tolerance);
        Assert.Equal(2.5, Start(layout, after), Tolerance);
        Assert.Equal(3.5, layout.DurationSeconds, Tolerance);
    }

    [Fact]
    public void AnAtWithoutChildren_HoldsTheSequenceForItsSeconds()
    {
        var after = new WaitCutsceneActionData { Seconds = 1f };
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.Add(new AtCutsceneActionData { Seconds = 2f });
        sequence.Actions.Add(after);

        var layout = CutsceneTimelineProjection.Build(sequence);

        Assert.Equal(2.0, Start(layout, after), Tolerance);
    }

    [Fact]
    public void NestedAts_AddUpTheirSeconds()
    {
        var leaf = new WaitCutsceneActionData { Seconds = 1f };
        var inner = new AtCutsceneActionData { Seconds = 0.5f };
        inner.Actions.Add(leaf);
        var outer = new AtCutsceneActionData { Seconds = 1f };
        outer.Actions.Add(inner);

        var layout = CutsceneTimelineProjection.Build(outer);

        Assert.Equal(1.5, Start(layout, leaf), Tolerance);
        Assert.Equal(2.5, layout.DurationSeconds, Tolerance);
    }

    [Fact]
    public void AnAtInARepeat_IsPlacedInTheLapDrawn()
    {
        var leaf = new WaitCutsceneActionData { Seconds = 1f };
        var at = new AtCutsceneActionData { Seconds = 0.5f };
        at.Actions.Add(leaf);
        var repeat = new RepeatCutsceneActionData { Count = 3 };
        repeat.Actions.Add(at);

        var layout = CutsceneTimelineProjection.Build(repeat);

        Assert.Equal(0.5, Start(layout, leaf), Tolerance);
        var lap = Assert.Single(layout.Items, item => item.Role == CutsceneTimelineItemRole.RepeatLap);
        Assert.Equal(1.5, lap.DurationSeconds, Tolerance);
        Assert.Equal(4.5, layout.DurationSeconds, Tolerance);
    }

    [Fact]
    public void ANegativeOrNotFiniteSeconds_IsPlacedAsZero()
    {
        var leaf = new WaitCutsceneActionData { Seconds = 1f };
        var at = new AtCutsceneActionData { Seconds = -3f };
        at.Actions.Add(leaf);

        Assert.Equal(0, Start(CutsceneTimelineProjection.Build(at), leaf), Tolerance);

        at.Seconds = float.NaN;
        Assert.Equal(0, Start(CutsceneTimelineProjection.Build(at), leaf), Tolerance);
    }

    // ---- Helpers ----

    private static double Start(CutsceneTimelineLayout layout, CutsceneActionData action)
        => Assert.Single(layout.Items, item => ReferenceEquals(item.Action, action)).StartSeconds;

    private static string Describe(CutsceneActionData action)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(new CutsceneAsset { RootAction = action }, node);
        return node["root_action"]!.ToString();
    }

    private static JObject Root(JObject action) => new()
    {
        ["id"] = Guid.NewGuid().ToString(),
        ["name"] = "Fixture",
        ["type"] = "CutsceneAsset",
        ["version"] = 1,
        ["root_action"] = action,
    };
}
