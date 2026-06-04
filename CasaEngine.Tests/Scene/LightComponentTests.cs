using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

public class LightComponentTests
{
    [Fact]
    public void Clone_CopiesCastShadows()
    {
        var component = new LightComponent
        {
            Type = LightType.Spot,
            CastShadows = true,
            Intensity = 2.0f,
        };

        var clone = component.Clone();

        Assert.True(clone.CastShadows);
        Assert.Equal(component.Type, clone.Type);
        Assert.Equal(component.Intensity, clone.Intensity);
    }

    [Fact]
    public void Load_UsesFalseWhenCastShadowsMissing()
    {
        var component = new LightComponent();
        var node = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "TestLight",
            ["coordinates"] = new JObject
            {
                ["position"] = new JObject
                {
                    ["x"] = 0.0f,
                    ["y"] = 0.0f,
                    ["z"] = 0.0f,
                },
                ["scale"] = new JObject
                {
                    ["x"] = 1.0f,
                    ["y"] = 1.0f,
                    ["z"] = 1.0f,
                },
                ["rotation"] = new JObject
                {
                    ["x"] = 0.0f,
                    ["y"] = 0.0f,
                    ["z"] = 0.0f,
                    ["w"] = 1.0f,
                },
            },
            ["children_component"] = new JArray(),
            ["light_type"] = LightType.Directional.ToString(),
            ["intensity"] = 1.0f,
        };

        component.Load(node);

        Assert.False(component.CastShadows);
    }

    [Fact]
    public void CoordinatesMutation_MarksComponentBoundsDirty()
    {
        var component = new LightComponent();
        component.ClearBoundingBoxDirtyRecursive();

        component.LocalTransform.Position = new Vector3(1.0f, 2.0f, 3.0f);

        Assert.True(component.IsBoundingBoxDirty);
    }

    [Fact]
    public void CoordinatesMutation_IgnoresUnchangedValues()
    {
        var component = new LightComponent();
        int positionChangedCount = 0;
        component.LocalTransform.PositionChanged += (_, _) => positionChangedCount++;
        component.ClearBoundingBoxDirtyRecursive();

        component.LocalTransform.Position = component.LocalTransform.Position;

        Assert.False(component.IsBoundingBoxDirty);
        Assert.Equal(0, positionChangedCount);
    }

    [Fact]
    public void LoadedCoordinatesMutation_MarksComponentBoundsDirty()
    {
        var component = new LightComponent();
        var node = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "TestLight",
            ["coordinates"] = new JObject
            {
                ["position"] = new JObject
                {
                    ["x"] = 0.0f,
                    ["y"] = 0.0f,
                    ["z"] = 0.0f,
                },
                ["scale"] = new JObject
                {
                    ["x"] = 1.0f,
                    ["y"] = 1.0f,
                    ["z"] = 1.0f,
                },
                ["rotation"] = new JObject
                {
                    ["x"] = 0.0f,
                    ["y"] = 0.0f,
                    ["z"] = 0.0f,
                    ["w"] = 1.0f,
                },
            },
            ["children_component"] = new JArray(),
            ["light_type"] = LightType.Point.ToString(),
        };
        component.Load(node);
        component.ClearBoundingBoxDirtyRecursive();

        component.LocalTransform.Position = new Vector3(4.0f, 5.0f, 6.0f);

        Assert.True(component.IsBoundingBoxDirty);
    }

    [Theory]
    [InlineData(LightType.Directional)]
    [InlineData(LightType.Point)]
    [InlineData(LightType.Spot)]
    public void AppendLights_PropagatesCastShadowsToVisibleLight(LightType type)
    {
        var component = new LightComponent
        {
            Type = type,
            CastShadows = true,
            Intensity = 1.0f,
            Range = 10.0f,
            InnerConeAngleDegrees = 20.0f,
            OuterConeAngleDegrees = 30.0f,
        };

        var context = new LightingContext();
        context.BeginCollection(Vector3.Zero, Vector3.Zero);

        component.AppendLights(context);

        switch (type)
        {
            case LightType.Directional:
                Assert.Equal(1, context.ActiveDirectionalLightCount);
                Assert.True(context.DirectionalLights[0].CastShadows);
                break;

            case LightType.Point:
                Assert.Equal(1, context.ActivePointLightCount);
                Assert.True(context.PointLights[0].CastShadows);
                break;

            case LightType.Spot:
                Assert.Equal(1, context.ActiveSpotLightCount);
                Assert.True(context.SpotLights[0].CastShadows);
                break;
        }
    }
}