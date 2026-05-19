using CasaEngine.Framework.Rendering.Shadows;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ForwardShadowResourcesTests
{
    [Fact]
    public void ShadowSettings_DefaultsMatchDisabledV1Setup()
    {
        var settings = new ShadowSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(1024, settings.Resolution);
        Assert.Equal(0.001f, settings.DepthBias);
        Assert.Equal(0.0f, settings.NormalBias);
        Assert.Equal(100.0f, settings.MaxDistance);
    }

    [Fact]
    public void Clear_RemovesVisibleLightsWithoutReplacingStorage()
    {
        var resources = new ForwardShadowResources();
        var visibleLightsReference = resources.VisibleLights;
        resources.Settings.Enabled = true;
        resources.AddVisibleLight(new ShadowLight(
            ShadowLightType.Directional,
            lightIndex: 0,
            lightViewProjection: Matrix.Identity,
            atlasViewport: new Rectangle(0, 0, 512, 512),
            depthBias: 0.002f,
            normalBias: 0.01f));

        resources.Clear();

        Assert.Same(visibleLightsReference, resources.VisibleLights);
        Assert.Equal(0, resources.VisibleLightCount);
        Assert.Null(resources.ShadowMapAtlas);
        Assert.True(resources.Settings.Enabled);
    }
}