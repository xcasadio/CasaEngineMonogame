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
    public void Parse_LogsFilter_EnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--project", "Projects/SampleProject/SampleProject.json",
            "--logs-filter", "Warning",
        });

        Assert.True(options.HasAutomation);
        Assert.Equal("Warning", options.LogsFilter);
    }

    [Fact]
    public void Parse_AutomationTimeout_DefaultsTo300Seconds_AndDoesNotEnableAutomationAlone()
    {
        Assert.Equal(300.0, EditorAutomationOptions.Parse(System.Array.Empty<string>()).AutomationTimeoutSeconds);

        var options = EditorAutomationOptions.Parse(new[] { "--automation-timeout", "5" });

        Assert.Equal(5.0, options.AutomationTimeoutSeconds);
        Assert.False(options.HasAutomation);
        Assert.Equal(1.0, EditorAutomationOptions.Parse(new[] { "--automation-timeout", "0.2" }).AutomationTimeoutSeconds);
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
    public void Parse_SetStaticModelMaterial_SplitsTheMeshIndexAtTheFirstEquals_AndEnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "Entities/Box.staticModel",
            "--set-static-model-material", "0=afcdcca9-8833-4f9c-9b13-3972177ba7b1",
        });

        Assert.Equal("0", options.SetStaticModelMaterialMeshIndex);
        Assert.Equal("afcdcca9-8833-4f9c-9b13-3972177ba7b1", options.SetStaticModelMaterialId);
        Assert.True(options.HasAutomation);
    }

    [Fact]
    public void Parse_SetStaticModelMaterial_KeepsWhatIsTyped_SoTheEditorCanRefuseIt()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--set-static-model-material", "five=not-a-guid",
        });

        Assert.Equal("five", options.SetStaticModelMaterialMeshIndex);
        Assert.Equal("not-a-guid", options.SetStaticModelMaterialId);
        Assert.True(options.HasAutomation);
    }

    [Theory]
    [InlineData("0")]      // no '='
    [InlineData("=afcdcca9-8833-4f9c-9b13-3972177ba7b1")]  // empty mesh index
    [InlineData("0=")]     // empty id
    public void Parse_SetStaticModelMaterial_Malformed_IsIgnored(string value)
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--set-static-model-material", value,
        });

        Assert.Null(options.SetStaticModelMaterialMeshIndex);
        Assert.Null(options.SetStaticModelMaterialId);
        Assert.False(options.HasAutomation);
    }

    [Fact]
    public void Parse_SetTextureSampler_SplitsTheKeyAtTheFirstEquals_AndEnablesAutomation()
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--open-asset", "UI/Sprites/Ui.Menu.ButtonsTexture.texture",
            "--set-texture-sampler", "address_u=Wrap",
        });

        Assert.Equal("address_u", options.SetTextureSamplerKey);
        Assert.Equal("Wrap", options.SetTextureSamplerValue);
        Assert.True(options.HasAutomation);
    }

    [Theory]
    [InlineData("address_u")]      // no '='
    [InlineData("=Wrap")]          // empty key
    [InlineData("address_u=")]     // empty value
    public void Parse_SetTextureSampler_Malformed_IsIgnored(string value)
    {
        var options = EditorAutomationOptions.Parse(new[]
        {
            "--set-texture-sampler", value,
        });

        Assert.Null(options.SetTextureSamplerKey);
        Assert.Null(options.SetTextureSamplerValue);
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