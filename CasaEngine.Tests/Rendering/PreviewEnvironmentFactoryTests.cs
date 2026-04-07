using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class PreviewEnvironmentFactoryTests
{
    [Fact]
    public void CreateNeutralPreview_ReturnsDedicatedSolidColorEnvironment()
    {
        var backgroundColor = new Color(20, 22, 28);

        var environment = PreviewEnvironmentFactory.CreateNeutralPreview(backgroundColor);

        Assert.Equal(EnvironmentType.None, environment.Type);
        Assert.Equal(EnvironmentBackgroundMode.SolidColor, environment.BackgroundMode);
        Assert.Equal(backgroundColor, environment.BackgroundColor);
        Assert.Equal(EnvironmentResolver.LegacyAmbientColor, environment.AmbientColor);
        Assert.Equal(1.0f, environment.AmbientIntensity);
        Assert.Equal(1.0f, environment.SpecularIntensity);
        Assert.Equal(Guid.Empty, environment.EnvironmentAssetId);
        Assert.Equal(Guid.Empty, environment.BackgroundCubemapAssetId);
        Assert.Equal(Guid.Empty, environment.SpecularEnvironmentCubemapAssetId);
    }
}