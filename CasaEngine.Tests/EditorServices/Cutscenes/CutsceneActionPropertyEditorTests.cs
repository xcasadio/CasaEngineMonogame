using System.Globalization;
using System.Reflection;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneActionPropertyEditorTests
{
    private static readonly string[] ActionTypes = typeof(CutsceneActionTypes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    public static IEnumerable<object[]> EveryProperty()
    {
        foreach (string type in ActionTypes)
        {
            foreach (var property in CutsceneActionPropertyEditor.GetProperties(type))
            {
                yield return [type, property.Key];
            }
        }
    }

    [Fact]
    public void TheActionTypesAreAllFound()
    {
        Assert.True(ActionTypes.Length >= 22);
        Assert.Contains(CutsceneActionTypes.FadeScreen, ActionTypes);
        Assert.Contains(CutsceneActionTypes.PlaySound, ActionTypes);
    }

    [Fact]
    public void EveryActionType_HasEditablePropertiesOrANamedExemption()
    {
        foreach (string type in ActionTypes)
        {
            bool hasProperties = CutsceneActionPropertyEditor.GetProperties(type).Count > 0;
            bool isExempt = CutsceneActionPropertyEditor.ExemptActionTypes.Contains(type);

            Assert.True(hasProperties != isExempt, $"{type} must have editable properties, or be a named exemption, not both.");
        }

        Assert.Equal([CutsceneActionTypes.Sequence, CutsceneActionTypes.Parallel], CutsceneActionPropertyEditor.ExemptActionTypes);
    }

    [Fact]
    public void AnUnknownAction_IsExemptAndHasNoProperty()
    {
        var unknown = new UnknownCutsceneActionData("Wait");

        Assert.True(CutsceneActionPropertyEditor.IsExempt(unknown));
        Assert.Empty(CutsceneActionPropertyEditor.GetProperties(unknown));
        Assert.True(CutsceneActionPropertyEditor.IsExempt(new SequenceCutsceneActionData()));
        Assert.True(CutsceneActionPropertyEditor.IsExempt(new ParallelCutsceneActionData()));
        Assert.False(CutsceneActionPropertyEditor.IsExempt(new WaitCutsceneActionData()));
    }

    [Fact]
    public void TheProperties_AreTheKeysSaveWritesForAFullyFilledAction()
    {
        foreach (string type in ActionTypes)
        {
            var action = CreateAction(type);
            foreach (var property in CutsceneActionPropertyEditor.GetProperties(action))
            {
                property.SetValue(action, Sample(property));
            }

            var node = new JObject();
            CutsceneAssetJsonSerializer.Save(new CutsceneAsset { RootAction = action }, node);

            var saved = ((JObject)node["root_action"]!).Properties()
                .Select(property => property.Name)
                .Where(name => name is not "type" and not "actions")
                .Order()
                .ToArray();
            var editable = CutsceneActionPropertyEditor.GetProperties(action).Select(property => property.Key).Order().ToArray();

            Assert.True(saved.SequenceEqual(editable), $"{type}: Save writes [{string.Join(", ", saved)}], the editor offers [{string.Join(", ", editable)}].");
        }
    }

    [Theory]
    [MemberData(nameof(EveryProperty))]
    public void AValidValue_IsReadBackEqual_AndItsTextReadsBackEqual(string type, string key)
    {
        var action = CreateAction(type);
        var property = CutsceneActionPropertyEditor.FindProperty(action, key)!;

        foreach (object value in Samples(property))
        {
            property.SetValue(action, value);

            Assert.True(property.AreEqual(value, property.GetValue(action)), $"{type}.{key}: {value}");

            string text = property.GetText(action);
            Assert.True(property.TryParse(text, out object? parsed, out string error), $"{type}.{key}: '{text}' -> {error}");
            Assert.True(property.AreEqual(value, parsed), $"{type}.{key}: '{text}'");
        }
    }

    [Theory]
    [MemberData(nameof(EveryProperty))]
    public void AnInvalidText_IsRefusedWithAMessage_AndChangesNothing(string type, string key)
    {
        var action = CreateAction(type);
        var property = CutsceneActionPropertyEditor.FindProperty(action, key)!;
        if (property.Kind is CutsceneActionPropertyKind.String or CutsceneActionPropertyKind.StringList)
        {
            return;
        }

        property.SetValue(action, Sample(property));
        object? before = property.GetValue(action);

        Assert.False(property.TryParse("not a value", out object? value, out string error));
        Assert.Null(value);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.True(property.AreEqual(before, property.GetValue(action)));
    }

    [Fact]
    public void ANumber_IsWrittenWithoutLoss_InTheInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            var wait = new WaitCutsceneActionData();
            var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;

            foreach (float number in new[] { 0.1f, 1234.5678f, -2.5f, 1e-7f, 16777217f })
            {
                property.SetValue(wait, number);
                string text = property.GetText(wait);

                Assert.DoesNotContain(",", text);
                Assert.True(property.TryParse(text, out object? parsed, out _));
                Assert.Equal(number, (float)parsed!);
            }

            Assert.Equal("0.1", property.FormatValue(0.1f));
            Assert.True(property.TryParse("1.5", out object? invariant, out _));
            Assert.Equal(1.5f, (float)invariant!);
            Assert.False(property.TryParse("1,5", out _, out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e40")]
    [InlineData("-1e40")]
    [InlineData("")]
    [InlineData("  ")]
    public void ANumber_MustBeFinite_AndNotBlank(string text)
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new WaitCutsceneActionData(), "seconds")!;

        Assert.False(property.TryParse(text, out object? value, out string error));
        Assert.Null(value);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("1e40")]
    public void ANullableNumber_MustBeFinite_Too(string text)
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new MoveSpriteCutsceneActionData(), "speed")!;

        Assert.False(property.TryParse(text, out _, out _));
    }

    [Theory]
    [InlineData("5")]
    [InlineData("0")]
    [InlineData("additive")]
    [InlineData("Nope")]
    public void AnEnumeration_MustBeADefinedName_NotANumber(string text)
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new FadeScreenCutsceneActionData(), "blend_mode")!;

        Assert.False(property.TryParse(text, out _, out string error));
        Assert.Contains("Additive", error);
    }

    [Fact]
    public void AnEnumeration_ReadsItsExactName()
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new FadeScreenCutsceneActionData(), "blend_mode")!;

        Assert.True(property.TryParse(" Additive ", out object? value, out _));
        Assert.Equal(Framework.Rendering.Depth.SpriteBlendMode.Additive, value);
    }

    [Theory]
    [InlineData("a,,b")]
    [InlineData("a,")]
    [InlineData(",a")]
    [InlineData("a, ,b")]
    public void AList_HasNoEmptyEntry(string text)
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new ExitOnInputCutsceneActionData(), "inputs")!;

        Assert.False(property.TryParse(text, out object? value, out string error));
        Assert.Null(value);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void AList_ReadsItsEntries_AndABlankTextIsTheEmptyList()
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new ExitOnInputCutsceneActionData(), "inputs")!;

        Assert.True(property.TryParse("a, b ,c", out object? value, out _));
        Assert.Equal(["a", "b", "c"], (IEnumerable<string>)value!);

        Assert.True(property.TryParse("  ", out object? empty, out _));
        Assert.Empty((IEnumerable<string>)empty!);
    }

    [Fact]
    public void ABlankText_FollowsTheRuleOfItsProperty()
    {
        var showSprite = new ShowSpriteCutsceneActionData();
        var fadeScreen = new FadeScreenCutsceneActionData();
        var moveTo = new MoveToCutsceneActionData();
        var playMusic = new PlayMusicCutsceneActionData();
        var playSound = new PlaySoundCutsceneActionData();

        // Nullable and not a string: null.
        foreach (string key in new[] { "x", "color", "blend_mode" })
        {
            Assert.True(CutsceneActionPropertyEditor.FindProperty(showSprite, key)!.TryParse(" ", out object? value, out _));
            Assert.Null(value);
        }

        // A string: the empty string (P9), even when nullable.
        Assert.True(CutsceneActionPropertyEditor.FindProperty(showSprite, "entity")!.TryParse(string.Empty, out object? text, out _));
        Assert.Equal(string.Empty, text);

        // Not nullable: refused.
        Assert.False(CutsceneActionPropertyEditor.FindProperty(fadeScreen, "duration_seconds")!.TryParse(string.Empty, out _, out _));
        Assert.False(CutsceneActionPropertyEditor.FindProperty(fadeScreen, "r")!.TryParse(string.Empty, out _, out _));
        Assert.False(CutsceneActionPropertyEditor.FindProperty(fadeScreen, "blend_mode")!.TryParse(string.Empty, out _, out _));
        Assert.False(CutsceneActionPropertyEditor.FindProperty(moveTo, "destination")!.TryParse(string.Empty, out _, out _));
        Assert.False(CutsceneActionPropertyEditor.FindProperty(playMusic, "crossfade")!.TryParse(string.Empty, out _, out _));
        Assert.False(CutsceneActionPropertyEditor.FindProperty(playSound, "sound_asset_id")!.TryParse(string.Empty, out _, out _));
    }

    [Fact]
    public void ATextIsParsedByTheRulesOfItsKind()
    {
        var showSprite = new ShowSpriteCutsceneActionData();
        var fadeScreen = new FadeScreenCutsceneActionData();
        var moveTo = new MoveToCutsceneActionData();

        var color = CutsceneActionPropertyEditor.FindProperty(showSprite, "color")!;
        Assert.True(color.TryParse("1, 2, 3, 4", out object? rgba, out _));
        Assert.Equal(new Color(1, 2, 3, 4), rgba);
        Assert.True(color.TryParse("1, 2, 3", out object? rgb, out _));
        Assert.Equal(new Color(1, 2, 3, 255), rgb);
        Assert.False(color.TryParse("1, 2", out _, out _));
        Assert.False(color.TryParse("1, 2, 3, 256", out _, out _));
        Assert.False(color.TryParse("1, 2, 3, -1", out _, out _));
        Assert.False(color.TryParse("1, 2, 3, 4, 5", out _, out _));
        Assert.False(color.TryParse("1, 2, 3.5, 4", out _, out _));

        var vector = CutsceneActionPropertyEditor.FindProperty(moveTo, "destination")!;
        Assert.True(vector.TryParse("1, -2.5, 3", out object? position, out _));
        Assert.Equal(new Vector3(1f, -2.5f, 3f), position);
        Assert.False(vector.TryParse("1, 2", out _, out _));
        Assert.False(vector.TryParse("1, 2, NaN", out _, out _));

        var channel = CutsceneActionPropertyEditor.FindProperty(fadeScreen, "r")!;
        Assert.True(channel.TryParse("255", out object? maximum, out _));
        Assert.Equal((byte)255, maximum);
        Assert.False(channel.TryParse("256", out _, out _));
        Assert.False(channel.TryParse("-1", out _, out _));

        var frames = CutsceneActionPropertyEditor.FindProperty(new WaitFramesCutsceneActionData(), "frames")!;
        Assert.True(frames.TryParse("-3", out object? negative, out _));
        Assert.Equal(-3, negative);
        Assert.False(frames.TryParse("2.5", out _, out _));
        Assert.False(frames.TryParse("99999999999", out _, out _));

        var crossfade = CutsceneActionPropertyEditor.FindProperty(new PlayMusicCutsceneActionData(), "crossfade")!;
        Assert.True(crossfade.TryParse("false", out object? off, out _));
        Assert.Equal(false, off);
        Assert.False(crossfade.TryParse("yes", out _, out _));
        Assert.False(crossfade.TryParse("1", out _, out _));

        var identifier = CutsceneActionPropertyEditor.FindProperty(new PlaySoundCutsceneActionData(), "sound_asset_id")!;
        var guid = Guid.NewGuid();
        Assert.True(identifier.TryParse(guid.ToString(), out object? parsedGuid, out _));
        Assert.Equal(guid, parsedGuid);
        Assert.False(identifier.TryParse("12", out _, out _));
    }

    [Fact]
    public void AStringIsKeptAsTyped()
    {
        var property = CutsceneActionPropertyEditor.FindProperty(new OpenScreenCutsceneActionData(), "target")!;

        Assert.True(property.TryParse("  main title ", out object? value, out _));
        Assert.Equal("  main title ", value);
    }

    [Fact]
    public void ANullStringIsAcceptedOnWrite_AndHasAnEmptyText()
    {
        var action = new OpenScreenCutsceneActionData { Target = "title" };
        var property = CutsceneActionPropertyEditor.FindProperty(action, "target")!;

        property.SetValue(action, null);

        Assert.Null(action.Target);
        Assert.Equal(string.Empty, property.GetText(action));
    }

    [Fact]
    public void SetValue_RefusesAValueOfTheWrongType_OrANullForANonNullableProperty()
    {
        var wait = new WaitCutsceneActionData();
        var seconds = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;

        Assert.Throws<ArgumentException>(() => seconds.SetValue(wait, "1"));
        Assert.Throws<ArgumentException>(() => seconds.SetValue(wait, 1));
        Assert.Throws<ArgumentException>(() => seconds.SetValue(wait, null));
    }

    [Fact]
    public void AListValue_IsACopy_OnReadAndOnWrite()
    {
        var action = new ExitOnInputCutsceneActionData();
        action.Inputs.AddRange(["a", "b"]);
        var property = CutsceneActionPropertyEditor.FindProperty(action, "inputs")!;

        var captured = (List<string>)property.GetValue(action)!;
        var replacement = new List<string> { "c" };
        property.SetValue(action, replacement);
        replacement.Add("d");

        Assert.Equal(["a", "b"], captured);
        Assert.Equal(["c"], action.Inputs);
        Assert.Equal(["c"], (IEnumerable<string>)property.GetValue(action)!);
        Assert.NotSame(action.Inputs, property.GetValue(action));
    }

    [Fact]
    public void GetFormatErrors_ExistsForTheElevenActionClassesThatCarryOne()
    {
        string[] withErrors =
        [
            CutsceneActionTypes.WaitFrames, CutsceneActionTypes.FadeScreenFrames, CutsceneActionTypes.ShowSprite,
            CutsceneActionTypes.HideSprite, CutsceneActionTypes.PlayVideoSprite, CutsceneActionTypes.StartVideoSprite,
            CutsceneActionTypes.MoveSprite, CutsceneActionTypes.TintSprite, CutsceneActionTypes.Repeat,
            CutsceneActionTypes.At, CutsceneActionTypes.ExitOnInput,
        ];

        foreach (string type in ActionTypes)
        {
            var errors = CutsceneActionPropertyEditor.GetFormatErrors(CreateAction(type));

            Assert.Equal(withErrors.Contains(type), errors != null);
        }

        var action = new ShowSpriteCutsceneActionData();
        action.FormatErrors["x"] = "bad";
        Assert.Same(action.FormatErrors, CutsceneActionPropertyEditor.GetFormatErrors(action));
    }

    private static CutsceneActionData CreateAction(string type)
    {
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, new JObject { ["root_action"] = new JObject { ["type"] = type } });
        Assert.IsNotType<UnknownCutsceneActionData>(asset.RootAction);
        return asset.RootAction!;
    }

    private static IEnumerable<object> Samples(CutsceneActionProperty property)
    {
        switch (property.Kind)
        {
            case CutsceneActionPropertyKind.Number:
                yield return 0.1f;
                yield return 1234.5678f;
                yield return -3f;
                yield return 0f;
                break;
            case CutsceneActionPropertyKind.Integer:
                yield return 3;
                yield return 0;
                yield return -4;
                break;
            case CutsceneActionPropertyKind.Byte:
                yield return (byte)0;
                yield return (byte)200;
                break;
            case CutsceneActionPropertyKind.Boolean:
                yield return true;
                yield return false;
                break;
            case CutsceneActionPropertyKind.Guid:
                yield return Guid.NewGuid();
                yield return Guid.Empty;
                break;
            case CutsceneActionPropertyKind.Color:
                yield return new Color(10, 20, 30, 40);
                yield return Color.Transparent;
                break;
            case CutsceneActionPropertyKind.Vector3:
                yield return new Vector3(0.1f, -2.5f, 1234.5678f);
                break;
            case CutsceneActionPropertyKind.Enum:
                foreach (object name in Enum.GetValues(property.ValueType))
                {
                    yield return name;
                }

                break;
            case CutsceneActionPropertyKind.String:
                yield return "an entity";
                yield return string.Empty;
                break;
            default:
                yield return new List<string> { "a", "b" };
                yield return new List<string>();
                break;
        }
    }

    private static object Sample(CutsceneActionProperty property) => Samples(property).First();
}
