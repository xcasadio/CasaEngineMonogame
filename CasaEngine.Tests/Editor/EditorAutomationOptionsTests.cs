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
    public void Parse_SaveProject_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[] { "--save-project" });

        Assert.True(options.SaveProject);
        Assert.True(options.HasAutomation);
    }
}