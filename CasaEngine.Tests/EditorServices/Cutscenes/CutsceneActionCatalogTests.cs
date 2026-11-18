using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneActionCatalogTests
{
    private static readonly string[] AllTypes = typeof(CutsceneActionTypes)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    public static TheoryData<string> Types()
    {
        var data = new TheoryData<string>();
        foreach (string type in AllTypes)
        {
            data.Add(type);
        }

        return data;
    }

    [Fact]
    public void Kinds_ContainEveryActionTypeOnceWithACategory()
    {
        var kindTypes = CutsceneActionCatalog.Kinds.Select(kind => kind.Type).ToList();

        Assert.Equal(AllTypes.OrderBy(type => type), kindTypes.OrderBy(type => type));
        Assert.Equal(kindTypes.Count, kindTypes.Distinct().Count());
        Assert.All(CutsceneActionCatalog.Kinds, kind => Assert.False(string.IsNullOrWhiteSpace(kind.Category)));
        Assert.Equal("Sprites / MoveSprite", CutsceneActionCatalog.Kinds.Single(kind => kind.Type == "MoveSprite").DisplayName);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Create_GivesAnActionThatSavesAndLoadsBackWithoutFormatError(string type)
    {
        var action = CutsceneActionCatalog.Create(type);

        Assert.Equal(type, action.Type);

        var loaded = RoundTrip(action);
        Assert.Equal(type, loaded.Type);
        Assert.IsNotType<UnknownCutsceneActionData>(loaded);
        Assert.True(CutsceneActionPropertyEditor.GetFormatErrors(loaded) is null or { Count: 0 });
        Assert.Equal(Serialize(action), Serialize(loaded));
    }

    [Fact]
    public void Create_AppliesTheStartingValuesOfRepeatAndWaitFrames()
    {
        Assert.Equal(1, ((RepeatCutsceneActionData)CutsceneActionCatalog.Create("Repeat")).Count);
        Assert.Equal(1, ((WaitFramesCutsceneActionData)CutsceneActionCatalog.Create("WaitFrames")).Frames);
        Assert.Equal(1f, ((PlaySoundCutsceneActionData)CutsceneActionCatalog.Create("PlaySound")).Volume);
    }

    [Fact]
    public void Create_RefusesAnUnknownType()
    {
        Assert.Throws<ArgumentException>(() => CutsceneActionCatalog.Create("Dance"));
    }

    [Theory]
    [InlineData("Sequence", true)]
    [InlineData("Parallel", true)]
    [InlineData("Repeat", true)]
    [InlineData("Wait", false)]
    [InlineData("MoveSprite", false)]
    public void CanHaveChildren_IsTrueForContainersOnly(string type, bool expected)
    {
        var action = CutsceneActionCatalog.Create(type);

        Assert.Equal(expected, CutsceneActionCatalog.CanHaveChildren(action));
        Assert.Empty(CutsceneActionCatalog.GetChildren(action));
    }

    [Fact]
    public void GetChildren_ReturnsTheLiveListOfAContainer()
    {
        var sequence = new SequenceCutsceneActionData();
        var wait = new WaitCutsceneActionData();
        sequence.Actions.Add(wait);

        var children = CutsceneActionCatalog.GetChildren(sequence);

        Assert.Same(wait, Assert.Single(children));
    }

    [Fact]
    public void Clone_EqualsTheSourceAndSharesNothing()
    {
        var repeat = new RepeatCutsceneActionData { Count = 3 };
        var inner = new SequenceCutsceneActionData();
        inner.Actions.Add(new MoveSpriteCutsceneActionData { EntityName = "orb", X = 1, Y = 2, Speed = 3 });
        inner.Actions.Add(new WaitCutsceneActionData { Seconds = 0.5f });
        repeat.Actions.Add(inner);

        var clone = (RepeatCutsceneActionData)CutsceneActionCatalog.Clone(repeat);

        Assert.NotSame(repeat, clone);
        Assert.Equal(Serialize(repeat), Serialize(clone));
        Assert.NotSame(repeat.Actions, clone.Actions);
        var clonedInner = (SequenceCutsceneActionData)clone.Actions[0];
        Assert.NotSame(inner, clonedInner);
        Assert.NotSame(inner.Actions, clonedInner.Actions);
        Assert.NotSame(inner.Actions[0], clonedInner.Actions[0]);

        clonedInner.Actions.Clear();
        Assert.Equal(2, inner.Actions.Count);
    }

    [Fact]
    public void Describe_GivesAShortLineForEveryType()
    {
        var expected = new Dictionary<string, string>
        {
            ["Wait"] = "Wait  1.5 s",
            ["WaitFrames"] = "WaitFrames  1 frames",
            ["MoveTo"] = "MoveTo  (none) → (0, 0, 0)",
            ["NavigateTo"] = "NavigateTo  (none) → (0, 0, 0)",
            ["Sequence"] = "Sequence  0 actions",
            ["Parallel"] = "Parallel  0 actions",
            ["Repeat"] = "Repeat  ×1",
            ["PlaySound"] = "PlaySound  (none)  vol 1",
            ["PlayMusic"] = "PlayMusic  (none)  fade in 0 s",
            ["StopMusic"] = "StopMusic  fade out 0 s",
            ["FadeMusic"] = "FadeMusic  → 0 over 0 s",
            ["FadeScreen"] = "FadeScreen  → #000000 over 0 s",
            ["FadeScreenFrames"] = "FadeScreenFrames  ? → ?  ? frames",
            ["OpenScreen"] = "OpenScreen  (none)",
            ["PlayVideo"] = "PlayVideo  (none)",
            ["ChangeWorld"] = "ChangeWorld  (none)",
            ["ShowSprite"] = "ShowSprite  (none)",
            ["HideSprite"] = "HideSprite  (none)",
            ["MoveSprite"] = "MoveSprite  (none) → (?, ?) @ ?",
            ["TintSprite"] = "TintSprite  (none) → ? over ? s",
            ["PlayVideoSprite"] = "PlayVideoSprite  (none)",
            ["StartVideoSprite"] = "StartVideoSprite  (none)",
            ["ExitOnInput"] = "ExitOnInput  (none): (none) → (none)",
        };

        foreach (string type in AllTypes)
        {
            var action = CutsceneActionCatalog.Create(type);
            if (action is WaitCutsceneActionData wait)
            {
                wait.Seconds = 1.5f;
            }

            Assert.Equal(expected[type], CutsceneActionSummary.Describe(action));
        }
    }

    [Fact]
    public void Describe_ShowsTheValuesOfFilledActions()
    {
        Assert.Equal(
            "MoveSprite  orb-left → (-60, 35) @ 120",
            CutsceneActionSummary.Describe(new MoveSpriteCutsceneActionData { EntityName = "orb-left", X = -60, Y = 35, Speed = 120 }));
        Assert.Equal("Repeat  forever", CutsceneActionSummary.Describe(new RepeatCutsceneActionData { Count = 0 }));
        Assert.Equal("Repeat  ×3", CutsceneActionSummary.Describe(new RepeatCutsceneActionData { Count = 3 }));
        Assert.Equal(
            "FadeScreen  → #FF8000 over 2 s",
            CutsceneActionSummary.Describe(new FadeScreenCutsceneActionData { R = 255, G = 128, B = 0, DurationSeconds = 2 }));
        Assert.Equal(
            "TintSprite  orb → #FF000080 over 1 s",
            CutsceneActionSummary.Describe(new TintSpriteCutsceneActionData { EntityName = "orb", Color = new Color(255, 0, 0, 128), Duration = 1 }));
        Assert.Equal(
            "TintSprite  orb → #FF0000 over 1 s",
            CutsceneActionSummary.Describe(new TintSpriteCutsceneActionData { EntityName = "orb", Color = new Color(255, 0, 0, 255), Duration = 1 }));
        Assert.Equal("ShowSprite  orb (spark)", CutsceneActionSummary.Describe(new ShowSpriteCutsceneActionData { EntityName = "orb", Sprite = "spark" }));

        var exit = new ExitOnInputCutsceneActionData { Mapping = "pad", NextCutscene = "loop" };
        exit.Inputs.Add("A");
        exit.Inputs.Add("B");
        Assert.Equal("ExitOnInput  pad: A, B → loop", CutsceneActionSummary.Describe(exit));

        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.Add(new WaitCutsceneActionData());
        Assert.Equal("Sequence  1 action", CutsceneActionSummary.Describe(sequence));
        Assert.Equal("Dance  (unknown type)", CutsceneActionSummary.Describe(new UnknownCutsceneActionData("Dance")));
    }

    private static CutsceneActionData RoundTrip(CutsceneActionData action)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(new CutsceneAsset { RootAction = action }, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);
        return loaded.RootAction!;
    }

    private static string Serialize(CutsceneActionData action)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(new CutsceneAsset { RootAction = action }, node);
        return node["root_action"]!.ToString();
    }
}
