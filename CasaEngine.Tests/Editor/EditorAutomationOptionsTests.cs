using CasaEngine.Editor;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class EditorAutomationOptionsTests
{
    [Fact]
    public void Parse_ProjectAndOpenAsset_DoNotEnableAutomationByDefault()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--project", "Projects/SampleProject/SampleProject.json",
            "--open-asset", "Spritesheets/ryu_0_0.sprite",
        });

        Assert.True(options.HasProjectPath);
        Assert.False(options.HasAutomation);
        Assert.Equal("Projects/SampleProject/SampleProject.json", options.ProjectPath);
        Assert.Equal("Spritesheets/ryu_0_0.sprite", options.OpenAssetPath);
    }

    [Fact]
    public void Parse_DiagnosticsCapture_WithOpenAsset_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--project", "Projects/SampleProject/SampleProject.json",
            "--open-asset", "Spritesheets/ryu_0_0.sprite",
            "--diagnostics-out", "artifacts/validation/sprite-viewer-smoke.txt",
        });

        Assert.True(options.HasProjectPath);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_EntityIndexZero_StillEnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--project", "Projects/SampleProject/SampleProject.json",
            "--entity-index", "0",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal(0, options.EntityIndex);
    }

    [Fact]
    public void Parse_SetScreenProperty_SplitsNodePropertyAndValue()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--set-screen-property", "Label:Text=Updated",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal("Label", options.SetScreenPropertyNodeName);
        Assert.Equal("Text", options.SetScreenPropertyName);
        Assert.Equal("Updated", options.SetScreenPropertyValue);
    }

    [Fact]
    public void Parse_SetScreenProperty_ValueContainingEquals_KeepsWholeValue()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--set-screen-property", "Label:Text=a=b",
        });

        Assert.Equal("Label", options.SetScreenPropertyNodeName);
        Assert.Equal("Text", options.SetScreenPropertyName);
        Assert.Equal("a=b", options.SetScreenPropertyValue);
    }

    [Theory]
    [InlineData("LabelText=Updated")]      // no ':'
    [InlineData("Label:Text")]             // no '=' after the ':'
    [InlineData(":Text=Updated")]          // empty node name
    [InlineData("Label:=Updated")]         // empty property name
    [InlineData("Label:Text=")]            // empty value
    public void Parse_SetScreenProperty_Malformed_IsIgnored(string value)
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--set-screen-property", value,
        });

        Assert.Null(options.SetScreenPropertyNodeName);
        Assert.Null(options.SetScreenPropertyName);
        Assert.Null(options.SetScreenPropertyValue);
        Assert.False(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutscenePreviewPlay_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "Cutscenes/loop.cutscene",
            "--cutscene-preview-play",
        });

        Assert.True(options.CutscenePreviewPlay);
        Assert.True(options.HasAutomation);
        Assert.False(EditorAutomationOptions.Parse(new[] { "--open-asset", "Cutscenes/loop.cutscene" }).CutscenePreviewPlay);
    }

    [Fact]
    public void Parse_SaveProject_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--save-project" });

        Assert.True(options.SaveProject);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutsceneSelectActionPath_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "Cutscenes/loop.cutscene",
            "--cutscene-select-action", "root_action.actions[1].actions[2]",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal("root_action.actions[1].actions[2]", options.CutsceneSelectActionPath);
    }

    [Fact]
    public void Parse_CutsceneAddActionType_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--cutscene-add-action", "Wait",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal("Wait", options.CutsceneAddActionType);
    }

    [Fact]
    public void Parse_CutsceneReactivate_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--cutscene-reactivate",
        });

        Assert.True(options.HasAutomation);
        Assert.True(options.CutsceneReactivate);
    }

    [Fact]
    public void Parse_CutscenePreviewSeek_ReadsTheListInInvariantCulture()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "Cutscenes/loop.cutscene",
            "--cutscene-preview-seek", "4.5,0.5, 2",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal(new[] { 4.5, 0.5, 2.0 }, options.CutscenePreviewSeekSeconds);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,abc")]
    [InlineData("-1")]
    [InlineData("0.5,-0.1")]
    [InlineData("1,,2")]
    [InlineData("NaN")]
    [InlineData("")]
    public void Parse_CutscenePreviewSeek_UnreadableOrNegativeValue_IgnoresTheWholeOption(string value)
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-seek", value });

        Assert.Empty(options.CutscenePreviewSeekSeconds);
        Assert.False(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutscenePreviewStep_ReadsAPositiveInteger()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-step", "30" });

        Assert.True(options.HasAutomation);
        Assert.Equal(30, options.CutscenePreviewStepFrames);
    }

    [Fact]
    public void Parse_CutscenePreviewStep_ReadsANegativeInteger_AsFramesBack()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-step", "-3" });

        Assert.True(options.HasAutomation);
        Assert.Equal(-3, options.CutscenePreviewStepFrames);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1.5")]
    [InlineData("-1.5")]
    [InlineData("many")]
    public void Parse_CutscenePreviewStep_NotANonZeroInteger_IsIgnored(string value)
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-step", value });

        Assert.Equal(0, options.CutscenePreviewStepFrames);
        Assert.False(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutscenePreviewStop_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-stop" });

        Assert.True(options.CutscenePreviewStop);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutscenePreviewResume_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-resume" });

        Assert.True(options.CutscenePreviewResume);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutsceneEditAfterTransport_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-edit-after-transport" });

        Assert.True(options.CutsceneEditAfterTransport);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutsceneEditAfterTransport_KeepsTheEditsAndTheTransportOptions()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--cutscene-preview-seek", "0.5",
            "--cutscene-select-action", "root_action.actions[1].actions[3]",
            "--cutscene-add-action", "WaitFrames",
            "--cutscene-edit-after-transport",
            "--cutscene-preview-step", "-2",
            "--cutscene-preview-resume",
        });

        Assert.True(options.CutsceneEditAfterTransport);
        Assert.Equal(new[] { 0.5 }, options.CutscenePreviewSeekSeconds);
        Assert.Equal("root_action.actions[1].actions[3]", options.CutsceneSelectActionPath);
        Assert.Equal("WaitFrames", options.CutsceneAddActionType);
        Assert.Equal(-2, options.CutscenePreviewStepFrames);
        Assert.True(options.CutscenePreviewResume);
        Assert.False(options.CutscenePreviewStop);
    }

    [Fact]
    public void Parse_CutscenePreviewSound_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-sound" });

        Assert.True(options.CutscenePreviewSound);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutscenePreviewInput_ReadsTheName_AndEnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "Cutscenes/loop.cutscene",
            "--cutscene-preview-play",
            "--cutscene-preview-input", "exit",
            "--capture-delay", "1",
        });

        Assert.Equal("exit", options.CutscenePreviewInput);
        Assert.True(options.CutscenePreviewPlay);
        Assert.Equal(1d, options.CaptureDelaySeconds);
        Assert.True(options.HasAutomation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("--cutscene-preview-resume")]
    public void Parse_CutscenePreviewInput_WithoutAName_IsIgnored_AndDoesNotSwallowTheNextOption(string value)
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-input", value, "--cutscene-preview-stop" });

        Assert.Null(options.CutscenePreviewInput);
        Assert.True(options.CutscenePreviewStop);
        Assert.Equal(value == "--cutscene-preview-resume", options.CutscenePreviewResume);
    }

    [Fact]
    public void Parse_CutscenePreviewInput_AtTheEndOfTheArguments_IsIgnored()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-input" });

        Assert.Null(options.CutscenePreviewInput);
        Assert.False(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutscenePreviewInput_KeepsTheStop_ForTheEditorToApplyOnly()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-preview-input", "exit", "--cutscene-preview-stop" });

        Assert.Equal("exit", options.CutscenePreviewInput);
        Assert.True(options.CutscenePreviewStop);
    }

    [Fact]
    public void Parse_CutscenePreviewSoundAndInput_AreAbsentByDefault()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--open-asset", "Cutscenes/loop.cutscene" });

        Assert.False(options.CutscenePreviewSound);
        Assert.Null(options.CutscenePreviewInput);
    }

    [Fact]
    public void Parse_CutscenePreviewTransportOptions_AreAbsentByDefault()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--open-asset", "Cutscenes/loop.cutscene" });

        Assert.Empty(options.CutscenePreviewSeekSeconds);
        Assert.Equal(0, options.CutscenePreviewStepFrames);
        Assert.False(options.CutscenePreviewStop);
        Assert.False(options.CutscenePreviewResume);
        Assert.False(options.CutsceneEditAfterTransport);
    }

    [Fact]
    public void Parse_CutsceneEditOps_ReadsTheListInOrder_AndEnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "Cutscenes/loop.cutscene",
            "--cutscene-edit-ops", "select:root_action.actions[1].actions[4]; up ;wrap:Parallel;copy;paste",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal(
            new[] { "select", "up", "wrap", "copy", "paste" },
            Array.ConvertAll(options.CutsceneEditOperations, operation => operation.Name));
        Assert.Equal("root_action.actions[1].actions[4]", options.CutsceneEditOperations[0].Argument);
        Assert.Null(options.CutsceneEditOperations[1].Argument);
        Assert.Equal("up", options.CutsceneEditOperations[1].Text);
        Assert.Equal("Parallel", options.CutsceneEditOperations[2].Argument);
        Assert.Null(options.CutsceneEditOperations[2].Detail);
    }

    [Fact]
    public void Parse_CutsceneEditOps_AloneEnablesAutomation()
    {
        Assert.True(EditorAutomationOptions.Parse(new[] { "--cutscene-edit-ops", "undo" }).HasAutomation);
    }

    [Fact]
    public void Parse_CutsceneEditOps_MoveAndDragHoverSplitTheirArgumentAtTheLastColon()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--cutscene-edit-ops", "move:root_action.actions[1]:0;draghover:root_action.actions[2]:after;shortcut:paste;select:a:b",
        });

        var operations = options.CutsceneEditOperations;
        Assert.Equal(("move", "root_action.actions[1]", "0"), (operations[0].Name, operations[0].Argument, operations[0].Detail));
        Assert.Equal(("draghover", "root_action.actions[2]", "after"), (operations[1].Name, operations[1].Argument, operations[1].Detail));
        // The other operations keep everything after the first colon as their argument.
        Assert.Equal(("shortcut", "paste", (string?)null), (operations[2].Name, operations[2].Argument, operations[2].Detail));
        Assert.Equal(("select", "a:b", (string?)null), (operations[3].Name, operations[3].Argument, operations[3].Detail));
    }

    [Fact]
    public void Parse_CutsceneEditOps_KeepsAnUnknownOrMalformedOperation_WithoutDroppingTheOthers()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--cutscene-edit-ops", "up;frobnicate:3;move:root_action;;paste;",
        });

        var operations = options.CutsceneEditOperations;
        Assert.Equal(4, operations.Length);
        Assert.Equal("frobnicate", operations[1].Name);
        Assert.Equal("3", operations[1].Argument);
        // A move without a second colon has no index: it is kept and refused when it is applied.
        Assert.Equal("move", operations[2].Name);
        Assert.Equal("root_action", operations[2].Argument);
        Assert.Null(operations[2].Detail);
        Assert.Equal("paste", operations[3].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(";;")]
    public void Parse_CutsceneEditOps_BlankList_GivesNoOperation(string value)
    {
        Assert.Empty(EditorAutomationOptions.Parse(new[] { "--cutscene-edit-ops", value }).CutsceneEditOperations);
    }

    [Fact]
    public void Parse_CutsceneEditOps_IsAbsentByDefault()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--open-asset", "Cutscenes/loop.cutscene" });

        Assert.Empty(options.CutsceneEditOperations);
        Assert.False(options.HasAutomation);
    }

    [Fact]
    public void Parse_CutsceneEditOps_NamesAreCaseInsensitive()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--cutscene-edit-ops", "SELECT:root_action;DragHover:root_action:Into" });

        Assert.Equal("select", options.CutsceneEditOperations[0].Name);
        Assert.Equal("draghover", options.CutsceneEditOperations[1].Name);
        Assert.Equal("Into", options.CutsceneEditOperations[1].Detail);
    }

    [Fact]
    public void Parse_CutsceneEditOps_PlaceKeepsItsSecondsAsTheArgument_AndAddAtSplitsItsTypeAndSecondsAtTheLastColon()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--cutscene-edit-ops", "select:root_action.actions[1];place:1.6;addat:Wait:2;ADDAT:MoveSprite:0.5;addat:Wait",
        });

        var operations = options.CutsceneEditOperations;
        Assert.Equal(("place", "1.6", (string?)null), (operations[1].Name, operations[1].Argument, operations[1].Detail));
        Assert.Equal(("addat", "Wait", "2"), (operations[2].Name, operations[2].Argument, operations[2].Detail));
        Assert.Equal(("addat", "MoveSprite", "0.5"), (operations[3].Name, operations[3].Argument, operations[3].Detail));
        // Without a second colon there is no time: the operation is kept and refused when it is applied.
        Assert.Equal(("addat", "Wait", (string?)null), (operations[4].Name, operations[4].Argument, operations[4].Detail));
    }

    [Theory]
    [InlineData("1.6", 1.6)]
    [InlineData("0", 0)]
    [InlineData(" 2 ", 2)]
    [InlineData("-0.5", -0.5)]
    [InlineData("1e1", 10)]
    public void TryParseSeconds_ReadsAFiniteNumber_WithADotWhateverTheCulture(string text, double expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.True(CutsceneEditOperation.TryParseSeconds(text, out double seconds));
            Assert.Equal(expected, seconds, 6);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("x")]
    [InlineData("1,6")]
    [InlineData("1.6 s")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1:2")]
    public void TryParseSeconds_RefusesWhatIsNotAFiniteNumber(string? text)
    {
        Assert.False(CutsceneEditOperation.TryParseSeconds(text, out _));
    }
}
