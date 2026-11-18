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

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    [InlineData("many")]
    public void Parse_CutscenePreviewStep_NotAPositiveInteger_IsIgnored(string value)
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
    public void Parse_CutscenePreviewTransportOptions_AreAbsentByDefault()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--open-asset", "Cutscenes/loop.cutscene" });

        Assert.Empty(options.CutscenePreviewSeekSeconds);
        Assert.Equal(0, options.CutscenePreviewStepFrames);
        Assert.False(options.CutscenePreviewStop);
    }
}
