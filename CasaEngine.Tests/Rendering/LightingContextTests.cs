using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class LightingContextTests
{
    [Fact]
    public void DirectionalLightStorage_MatchesConfiguredForwardCap()
    {
        var lightingContext = new LightingContext();

        Assert.Equal(8, LightingContext.MaxDirectionalLights);
        Assert.Equal(LightingContext.MaxDirectionalLights, lightingContext.DirectionalLights.Length);
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(99, 8)]
    public void ClampActiveDirectionalLightCount_ClampsToSupportedRange(int requestedCount, int expectedCount)
    {
        int clampedCount = LightingContext.ClampActiveDirectionalLightCount(requestedCount);

        Assert.Equal(expectedCount, clampedCount);
    }

    [Fact]
    public void AddPointLight_WhenCapacityExceeded_KeepsHighestViewPriorityCandidates()
    {
        var context = new LightingContext();
        context.BeginCollection(Vector3.Zero, Vector3.Zero);

        for (int i = 0; i < LightingContext.MaxPointLights; i++)
        {
            context.AddPointLight(new PointLight(
                new Vector3(100.0f + i * 10.0f, 0.0f, 0.0f),
                Vector3.One,
                Vector3.One,
                range: 5.0f));
        }

        var importantLight = new PointLight(
            new Vector3(0.5f, 0.0f, 0.0f),
            Vector3.One,
            Vector3.One,
            range: 5.0f);

        context.AddPointLight(importantLight);

        Assert.Equal(LightingContext.MaxPointLights, context.ActivePointLightCount);
        Assert.Equal(importantLight.Position, context.PointLights[0].Position);
        Assert.False(ContainsPointLight(context, new Vector3(170.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void AddSpotLight_WhenCapacityExceeded_KeepsHighestViewPriorityCandidates()
    {
        var context = new LightingContext();
        context.BeginCollection(Vector3.Zero, Vector3.Zero);

        for (int i = 0; i < LightingContext.MaxSpotLights; i++)
        {
            context.AddSpotLight(new SpotLight(
                new Vector3(100.0f + i * 10.0f, 0.0f, 0.0f),
                Vector3.Forward,
                Vector3.One,
                Vector3.One,
                range: 5.0f,
                innerConeAngle: 0.25f,
                outerConeAngle: 0.5f));
        }

        var importantLight = new SpotLight(
            new Vector3(0.5f, 0.0f, 0.0f),
            Vector3.Forward,
            Vector3.One,
            Vector3.One,
            range: 5.0f,
            innerConeAngle: 0.25f,
            outerConeAngle: 0.5f);

        context.AddSpotLight(importantLight);

        Assert.Equal(LightingContext.MaxSpotLights, context.ActiveSpotLightCount);
        Assert.Equal(importantLight.Position, context.SpotLights[0].Position);
        Assert.False(ContainsSpotLight(context, new Vector3(170.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void BeginCollection_ResetsLocalLightSelectionScores()
    {
        var context = new LightingContext();
        context.BeginCollection(Vector3.Zero, Vector3.Zero);

        for (int i = 0; i < LightingContext.MaxPointLights; i++)
        {
            context.AddPointLight(new PointLight(
                new Vector3(i, 0.0f, 0.0f),
                Vector3.One,
                Vector3.One,
                range: 20.0f));
        }

        context.BeginCollection(Vector3.Zero, Vector3.One);
        var lowPriorityLight = new PointLight(
            new Vector3(500.0f, 0.0f, 0.0f),
            Vector3.One,
            Vector3.One,
            range: 1.0f);

        context.AddPointLight(lowPriorityLight);

        Assert.Equal(Vector3.One, context.AmbientColor);
        Assert.Equal(1, context.ActivePointLightCount);
        Assert.Equal(lowPriorityLight.Position, context.PointLights[0].Position);
    }

    [Fact]
    public void AddLight_IgnoresNonContributingLights()
    {
        var context = new LightingContext();
        context.BeginCollection(Vector3.Zero, Vector3.Zero);

        context.AddDirectionalLight(new DirectionalLight(Vector3.Forward, Vector3.Zero, Vector3.Zero));
        context.AddPointLight(new PointLight(Vector3.Zero, Vector3.Zero, Vector3.Zero, range: 10.0f));
        context.AddSpotLight(new SpotLight(
            Vector3.Zero,
            Vector3.Forward,
            Vector3.Zero,
            Vector3.Zero,
            range: 10.0f,
            innerConeAngle: 0.25f,
            outerConeAngle: 0.5f));

        Assert.Equal(0, context.ActiveDirectionalLightCount);
        Assert.Equal(0, context.ActivePointLightCount);
        Assert.Equal(0, context.ActiveSpotLightCount);
    }

    [Fact]
    public void CopyFrom_CopiesVisibleLightsAndAmbient()
    {
        var source = new LightingContext();
        source.BeginCollection(new Vector3(1.0f, 2.0f, 3.0f), new Vector3(0.2f, 0.3f, 0.4f));
        source.AddDirectionalLight(new DirectionalLight(Vector3.Normalize(new Vector3(1.0f, -2.0f, 0.5f)), Vector3.One, Vector3.One * 0.5f, intensity: 2.0f));
        source.AddPointLight(new PointLight(new Vector3(5.0f, 0.0f, 0.0f), Vector3.One, Vector3.One * 0.25f, range: 10.0f, intensity: 3.0f));
        source.AddSpotLight(new SpotLight(new Vector3(0.0f, 2.0f, 0.0f), Vector3.Forward, Vector3.One * 0.75f, Vector3.One, range: 15.0f, innerConeAngle: 0.2f, outerConeAngle: 0.4f, intensity: 1.5f));

        var destination = new LightingContext();
        destination.CopyFrom(source);

        Assert.Equal(source.AmbientColor, destination.AmbientColor);
        Assert.Equal(source.ActiveDirectionalLightCount, destination.ActiveDirectionalLightCount);
        Assert.Equal(source.ActivePointLightCount, destination.ActivePointLightCount);
        Assert.Equal(source.ActiveSpotLightCount, destination.ActiveSpotLightCount);
        Assert.Equal(source.DirectionalLights[0].Direction, destination.DirectionalLights[0].Direction);
        Assert.Equal(source.PointLights[0].Position, destination.PointLights[0].Position);
        Assert.Equal(source.SpotLights[0].Position, destination.SpotLights[0].Position);
    }

    private static bool ContainsPointLight(LightingContext context, Vector3 position)
    {
        for (int i = 0; i < context.ActivePointLightCount; i++)
        {
            if (context.PointLights[i].Position == position)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpotLight(LightingContext context, Vector3 position)
    {
        for (int i = 0; i < context.ActiveSpotLightCount; i++)
        {
            if (context.SpotLights[i].Position == position)
            {
                return true;
            }
        }

        return false;
    }
}