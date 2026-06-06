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
}