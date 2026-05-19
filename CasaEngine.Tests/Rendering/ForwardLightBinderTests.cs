using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ForwardLightBinderTests
{
    [Fact]
    public void CreateSnapshot_PacksActiveLightsAndZeroesInactiveSlots()
    {
        var lighting = new LightingContext();
        lighting.BeginCollection(Vector3.Zero, new Vector3(0.1f, 0.2f, 0.3f));
        lighting.AddDirectionalLight(new DirectionalLight(Vector3.UnitX, Vector3.One, Vector3.One * 0.5f, intensity: 2.0f));
        lighting.AddPointLight(new PointLight(new Vector3(1.0f, 2.0f, 3.0f), Vector3.One, Vector3.One * 0.25f, range: 4.0f, intensity: 3.0f));
        lighting.AddSpotLight(new SpotLight(new Vector3(4.0f, 5.0f, 6.0f), Vector3.UnitY, Vector3.One * 0.75f, Vector3.One, range: 7.0f, innerConeAngle: 0.25f, outerConeAngle: 0.5f, intensity: 1.5f));

        var snapshot = ForwardLightBinder.CreateSnapshot(lighting);

        Assert.Equal(1, snapshot.ActiveDirectionalLightCount);
        Assert.Equal(1, snapshot.ActivePointLightCount);
        Assert.Equal(1, snapshot.ActiveSpotLightCount);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), snapshot.AmbientColor);

        Assert.Equal(Vector3.UnitX, snapshot.DirectionalLightDirections[0]);
        Assert.Equal(Vector3.One * 2.0f, snapshot.DirectionalLightDiffuseColors[0]);
        Assert.Equal(Vector3.One, snapshot.DirectionalLightSpecularColors[0]);
        Assert.Equal(Vector3.Zero, snapshot.DirectionalLightDirections[1]);

        Assert.Equal(new Vector4(1.0f, 2.0f, 3.0f, 4.0f), snapshot.PointLightPositionAndRangeData[0]);
        Assert.Equal(new Vector4(3.0f, 3.0f, 3.0f, 0.0f), snapshot.PointLightDiffuseData[0]);
        Assert.Equal(new Vector4(0.75f, 0.75f, 0.75f, 0.0f), snapshot.PointLightSpecularData[0]);
        Assert.Equal(Vector4.Zero, snapshot.PointLightPositionAndRangeData[1]);

        Assert.Equal(new Vector4(4.0f, 5.0f, 6.0f, 7.0f), snapshot.SpotLightPositionAndRangeData[0]);
        Assert.Equal(new Vector4(0.0f, 1.0f, 0.0f, MathF.Cos(0.25f)), snapshot.SpotLightDirectionAndInnerConeCosData[0]);
        Assert.Equal(new Vector4(1.125f, 1.125f, 1.125f, 0.0f), snapshot.SpotLightDiffuseData[0]);
        Assert.Equal(new Vector4(1.5f, 1.5f, 1.5f, MathF.Cos(0.5f)), snapshot.SpotLightSpecularAndOuterConeCosData[0]);
        Assert.Equal(Vector4.Zero, snapshot.SpotLightPositionAndRangeData[1]);
    }

    [Fact]
    public void CreateSnapshot_WithNullLighting_ProducesZeroedBindings()
    {
        var snapshot = ForwardLightBinder.CreateSnapshot(null);

        Assert.Equal(0, snapshot.ActiveDirectionalLightCount);
        Assert.Equal(0, snapshot.ActivePointLightCount);
        Assert.Equal(0, snapshot.ActiveSpotLightCount);
        Assert.Equal(Vector3.Zero, snapshot.AmbientColor);
        Assert.Equal(Vector3.Zero, snapshot.DirectionalLightDirections[0]);
        Assert.Equal(Vector4.Zero, snapshot.PointLightPositionAndRangeData[0]);
        Assert.Equal(Vector4.Zero, snapshot.SpotLightPositionAndRangeData[0]);
    }
}