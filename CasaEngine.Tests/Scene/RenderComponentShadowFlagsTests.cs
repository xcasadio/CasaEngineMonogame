using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

public class RenderComponentShadowFlagsTests
{
    [Fact]
    public void Clone_CopiesPrimitiveShadowFlags()
    {
        var component = new StaticModelComponent
        {
            CastShadows = false,
            ReceiveShadows = false,
        };

        var clone = component.Clone();

        Assert.False(clone.CastShadows);
        Assert.False(clone.ReceiveShadows);
    }

    [Fact]
    public void StaticModelLoad_UsesTrueShadowDefaultsWhenFlagsMissing()
    {
        var component = new StaticModelComponent();

        component.Load(CreateSceneComponentNode());

        Assert.True(component.CastShadows);
        Assert.True(component.ReceiveShadows);
    }

    [Fact]
    public void SkinnedMeshLoad_UsesSerializedShadowFlags()
    {
        var component = new SkinnedMeshComponent();
        var node = CreateSceneComponentNode();
        node["cast_shadows"] = false;
        node["receive_shadows"] = false;

        component.Load(node);

        Assert.False(component.CastShadows);
        Assert.False(component.ReceiveShadows);
    }

    private static JObject CreateSceneComponentNode()
    {
        return new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Component",
            ["coordinates"] = new JObject
            {
                ["position"] = CreateVector3Node(0.0f, 0.0f, 0.0f),
                ["scale"] = CreateVector3Node(1.0f, 1.0f, 1.0f),
                ["rotation"] = new JObject
                {
                    ["x"] = 0.0f,
                    ["y"] = 0.0f,
                    ["z"] = 0.0f,
                    ["w"] = 1.0f,
                },
            },
            ["children_component"] = new JArray(),
        };
    }

    private static JObject CreateVector3Node(float x, float y, float z)
    {
        return new JObject
        {
            ["x"] = x,
            ["y"] = y,
            ["z"] = z,
        };
    }
}