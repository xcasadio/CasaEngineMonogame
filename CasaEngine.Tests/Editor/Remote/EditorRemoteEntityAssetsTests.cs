using CasaEngine.Editor.Remote;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>Plan P13, security review S3-1: the rule that an entity asset panel holds the resolved file. No file is touched.</summary>
public sealed class EditorRemoteEntityAssetsTests
{
    private static readonly string Project = Path.Combine(Path.GetTempPath(), "casa-entity-assets-test", "project");

    private static readonly string Expected = Path.Combine(Project, "Entities", "Box.entity");

    [Theory]
    [InlineData("Entities/Box.entity")]
    [InlineData("Entities\\Box.entity")]
    public void IsLoadedFile_TheSameFile_IsTrue(string loaded)
    {
        Assert.True(EditorRemoteEntityAssets.IsLoadedFile(Project, loaded, Expected));
    }

    [Fact]
    public void IsLoadedFile_TheSameFileInAnotherCase_IsTrue()
    {
        Assert.True(EditorRemoteEntityAssets.IsLoadedFile(Project, "entities/BOX.Entity", Expected));
        Assert.True(EditorRemoteEntityAssets.IsLoadedFile(Project.ToUpperInvariant(), "Entities/Box.entity", Expected));
    }

    [Theory]
    [InlineData("Entities/BoxCopy.entity")]
    [InlineData("Box.entity")]
    [InlineData("Entities/Box.entity.bak")]
    public void IsLoadedFile_AnotherFile_IsFalse(string loaded)
    {
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, loaded, Expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLoadedFile_NoLoadedPath_IsFalse(string loaded)
    {
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, loaded, Expected));
    }

    [Fact]
    public void IsLoadedFile_RootedLoadedPath_IsFalse_EvenForTheExpectedFile()
    {
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, Expected, Expected));
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, "/Entities/Box.entity", Expected));
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, "\\Entities\\Box.entity", Expected));
    }

    [Theory]
    [InlineData("Entities/../Entities/Box.entity")]
    [InlineData("Entities\\..\\Entities\\Box.entity")]
    [InlineData("../project/Entities/Box.entity")]
    [InlineData("..")]
    public void IsLoadedFile_PathWithParentSegment_IsFalse_EvenWhenItNormalisesToTheExpectedFile(string loaded)
    {
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, loaded, Expected));
    }

    [Fact]
    public void IsLoadedFile_NoProjectOrExpectedPath_IsFalse()
    {
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(null, "Entities/Box.entity", Expected));
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(" ", "Entities/Box.entity", Expected));
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, "Entities/Box.entity", null));
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, "Entities/Box.entity", ""));
    }

    [Fact]
    public void IsLoadedFile_PathTheSystemCannotNormalise_IsFalse_WithoutThrowing()
    {
        Assert.False(EditorRemoteEntityAssets.IsLoadedFile(Project, "Entities/Box\0.entity", Expected));
    }
}
