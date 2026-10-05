using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioBackendSelectionTests
{
    [Fact]
    public void Environment_Software_OverridesProjectMonoGame()
    {
        var result = AudioBackendSelection.Resolve("Software", AudioBackendKind.MonoGame);

        Assert.Equal(AudioBackendKind.Software, result.Kind);
        Assert.Equal(AudioBackendSource.Environment, result.Source);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Environment_MonoGame_OverridesProjectSoftware()
    {
        var result = AudioBackendSelection.Resolve("MonoGame", AudioBackendKind.Software);

        Assert.Equal(AudioBackendKind.MonoGame, result.Kind);
        Assert.Equal(AudioBackendSource.Environment, result.Source);
    }

    [Fact]
    public void NoEnvironmentAndNoProjectSetting_UsesTheDefault()
    {
        var result = AudioBackendSelection.Resolve(null, null);

        Assert.Equal(AudioBackendSelection.DefaultKind, result.Kind);
        Assert.Equal(AudioBackendSource.Default, result.Source);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void ProjectLoadedWithoutTheSetting_UsesTheDefault()
    {
        var result = AudioBackendSelection.Resolve(string.Empty, projectSetting: null);

        Assert.Equal(AudioBackendSelection.DefaultKind, result.Kind);
        Assert.Equal(AudioBackendSource.Default, result.Source);
    }

    [Fact]
    public void ProjectSoftware_IsChosenWithProjectSource()
    {
        var result = AudioBackendSelection.Resolve(null, AudioBackendKind.Software);

        Assert.Equal(AudioBackendKind.Software, result.Kind);
        Assert.Equal(AudioBackendSource.Project, result.Source);
    }

    [Fact]
    public void ProjectMonoGame_Explicit_IsChosenWithProjectSource()
    {
        var result = AudioBackendSelection.Resolve(null, AudioBackendKind.MonoGame);

        Assert.Equal(AudioBackendKind.MonoGame, result.Kind);
        Assert.Equal(AudioBackendSource.Project, result.Source);
    }

    [Theory]
    [InlineData("software")]
    [InlineData("SOFTWARE")]
    [InlineData("  Software ")]
    public void Environment_IsCaseInsensitive(string value)
    {
        var result = AudioBackendSelection.Resolve(value, AudioBackendKind.MonoGame);

        Assert.Equal(AudioBackendKind.Software, result.Kind);
        Assert.Equal(AudioBackendSource.Environment, result.Source);
    }

    [Theory]
    [InlineData("OpenAL")]
    [InlineData("1")]
    public void UnknownEnvironment_WarnsAndFallsThroughToTheProject(string value)
    {
        var result = AudioBackendSelection.Resolve(value, AudioBackendKind.Software);

        Assert.Equal(AudioBackendKind.Software, result.Kind);
        Assert.Equal(AudioBackendSource.Project, result.Source);
        Assert.Contains(value, result.Warning);
    }

    [Fact]
    public void UnknownEnvironment_WarnsAndFallsThroughToTheDefault()
    {
        var result = AudioBackendSelection.Resolve("OpenAL", null);

        Assert.Equal(AudioBackendSelection.DefaultKind, result.Kind);
        Assert.Equal(AudioBackendSource.Default, result.Source);
        Assert.Contains("OpenAL", result.Warning);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceEnvironment_IsIgnoredWithoutWarning(string value)
    {
        var result = AudioBackendSelection.Resolve(value, AudioBackendKind.Software);

        Assert.Equal(AudioBackendSource.Project, result.Source);
        Assert.Null(result.Warning);
    }
}
