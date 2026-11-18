using System.Reflection;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneActionPropertyMetadataTests
{
    private static readonly string[] ActionTypes = typeof(CutsceneActionTypes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    private sealed record Expected(
        CutsceneActionPropertySemantic Semantic = CutsceneActionPropertySemantic.Plain,
        double? Minimum = null,
        double? Maximum = null,
        bool IsMinimumExclusive = false);

    private static readonly Dictionary<string, Expected> ExpectedByProperty = new()
    {
        ["Wait.seconds"] = new(Minimum: 0),
        ["MoveTo.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["MoveTo.stopping_distance"] = new(Minimum: 0),
        ["MoveTo.timeout_seconds"] = new(Minimum: 0),
        ["NavigateTo.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["NavigateTo.stopping_distance"] = new(Minimum: 0),
        ["NavigateTo.timeout_seconds"] = new(Minimum: 0),
        ["Repeat.count"] = new(Minimum: 0),
        ["PlaySound.sound_asset_id"] = new(CutsceneActionPropertySemantic.Sound),
        ["PlaySound.volume"] = new(Minimum: 0, Maximum: 1),
        ["PlayMusic.sound_asset_id"] = new(CutsceneActionPropertySemantic.Sound),
        ["PlayMusic.fade_in_seconds"] = new(Minimum: 0),
        ["StopMusic.fade_out_seconds"] = new(Minimum: 0),
        ["FadeMusic.target_volume"] = new(Minimum: 0, Maximum: 1),
        ["FadeMusic.duration_seconds"] = new(Minimum: 0),
        ["FadeScreen.duration_seconds"] = new(Minimum: 0),
        ["WaitFrames.frames"] = new(Minimum: 0),
        ["FadeScreenFrames.frames"] = new(Minimum: 0),
        ["OpenScreen.target"] = new(CutsceneActionPropertySemantic.Screen),
        ["PlayVideo.target"] = new(CutsceneActionPropertySemantic.Screen),
        ["ChangeWorld.target"] = new(CutsceneActionPropertySemantic.World),
        ["ShowSprite.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["ShowSprite.sprite"] = new(CutsceneActionPropertySemantic.Sprite),
        ["HideSprite.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["PlayVideoSprite.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["StartVideoSprite.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["MoveSprite.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["MoveSprite.speed"] = new(Minimum: 0, IsMinimumExclusive: true),
        ["TintSprite.entity"] = new(CutsceneActionPropertySemantic.Entity),
        ["TintSprite.duration"] = new(Minimum: 0),
        ["ExitOnInput.mapping"] = new(CutsceneActionPropertySemantic.ButtonsMapping),
        ["ExitOnInput.inputs"] = new(CutsceneActionPropertySemantic.InputList),
        ["ExitOnInput.next_cutscene"] = new(CutsceneActionPropertySemantic.Cutscene),
    };

    [Fact]
    public void EveryProperty_HasTheExpectedMeaningAndLimits()
    {
        var seen = new HashSet<string>();
        foreach (string type in ActionTypes)
        {
            foreach (var property in CutsceneActionPropertyEditor.GetProperties(type))
            {
                string name = $"{type}.{property.Key}";
                var expected = ExpectedByProperty.GetValueOrDefault(name) ?? new Expected();
                seen.Add(name);

                Assert.True(expected.Semantic == property.Semantic, $"{name}: semantic {property.Semantic}.");
                Assert.True(expected.Minimum == property.Minimum, $"{name}: minimum {property.Minimum}.");
                Assert.True(expected.Maximum == property.Maximum, $"{name}: maximum {property.Maximum}.");
                Assert.True(expected.IsMinimumExclusive == property.IsMinimumExclusive, $"{name}: exclusive minimum.");
            }
        }

        foreach (string name in ExpectedByProperty.Keys)
        {
            Assert.Contains(name, seen);
        }
    }

    [Fact]
    public void ADeclaredLimit_IsOnANumericProperty()
    {
        foreach (string type in ActionTypes)
        {
            foreach (var property in CutsceneActionPropertyEditor.GetProperties(type))
            {
                if (property.Minimum != null || property.Maximum != null)
                {
                    Assert.True(
                        property.Kind is CutsceneActionPropertyKind.Number or CutsceneActionPropertyKind.Integer,
                        $"{type}.{property.Key} has a limit but is {property.Kind}.");
                }
            }
        }
    }

    public static IEnumerable<object[]> LimitedProperties()
    {
        foreach (string type in ActionTypes)
        {
            foreach (var property in CutsceneActionPropertyEditor.GetProperties(type))
            {
                if (property.Minimum != null || property.Maximum != null)
                {
                    yield return [type, property.Key];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(LimitedProperties))]
    public void TheDeclaredLimits_MatchTheValidator(string type, string key)
    {
        var property = CutsceneActionPropertyEditor.GetProperties(type).Single(candidate => candidate.Key == key);

        if (property.Minimum is { } minimum)
        {
            double outside = property.Kind == CutsceneActionPropertyKind.Integer ? minimum - 1 : minimum - 0.5;
            Assert.True(Reports(type, property, outside), $"{type}.{key} below the minimum must be reported.");

            Assert.Equal(property.IsMinimumExclusive, Reports(type, property, minimum));
            if (property.IsMinimumExclusive)
            {
                Assert.False(Reports(type, property, minimum + 0.001), $"{type}.{key} just above an exclusive minimum is valid.");
            }
        }

        if (property.Maximum is { } maximum)
        {
            Assert.True(Reports(type, property, maximum + 0.5), $"{type}.{key} above the maximum must be reported.");
            Assert.False(Reports(type, property, maximum), $"{type}.{key} at the maximum is valid.");
        }
    }

    [Fact]
    public void AnUnlimitedNumericProperty_IsNeverReportedForItsValue()
    {
        var action = new ShowSpriteCutsceneActionData { EntityName = "e" };
        var x = CutsceneActionPropertyEditor.FindProperty(action, "x")!;

        Assert.Null(x.Minimum);
        Assert.Null(x.Maximum);
        x.SetValue(action, -1000f);
        Assert.DoesNotContain(ValidateMessages(action), message => message.StartsWith("ShowSprite.x ", StringComparison.Ordinal) && !message.Contains("together", StringComparison.Ordinal));
    }

    private static bool Reports(string type, CutsceneActionProperty property, double value)
    {
        var action = Create(type);
        object boxed = property.Kind == CutsceneActionPropertyKind.Integer ? (object)(int)value : (float)value;
        property.SetValue(action, boxed);

        string prefix = $"{type}.{property.Key} ";
        return ValidateMessages(action).Any(message => message.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static List<string> ValidateMessages(CutsceneActionData action)
    {
        var asset = new CutsceneAsset { RootAction = action };
        return CutsceneValidator.Validate(asset).Messages.Select(message => message.Message).ToList();
    }

    private static CutsceneActionData Create(string type)
    {
        var classType = typeof(CutsceneActionData).Assembly.GetType($"CasaEngine.Framework.Cutscenes.{type}CutsceneActionData", throwOnError: true)!;
        return (CutsceneActionData)Activator.CreateInstance(classType)!;
    }
}
