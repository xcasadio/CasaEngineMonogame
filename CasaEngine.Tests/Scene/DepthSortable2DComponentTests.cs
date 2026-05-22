using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

public sealed class DepthSortable2DComponentTests
{
    [Fact]
    public void BuildSortKey_UsesSortAnchorAndTopDownYUpMode()
    {
        var component = new DepthSortable2DComponent
        {
            RenderPass = RenderPass2D.YSortedWorld,
            SortingLayer = 12,
            OrderInLayer = 3,
            Elevation = 1,
            SortAnchorLocal = new Vector2(4f, -8f),
            LocalSortOffset = 2,
            StableId = 99,
        };

        var sortKey = component.BuildSortKey(new Vector3(10f, -20f, 0f));

        Assert.Equal((int)RenderPass2D.YSortedWorld, sortKey.RenderPass);
        Assert.Equal(12, sortKey.SortingLayer);
        Assert.Equal(3, sortKey.OrderInLayer);
        Assert.Equal(1, sortKey.Elevation);
        Assert.Equal(2800, sortKey.SortCoordinate);
        Assert.Equal(2, sortKey.LocalSortOffset);
        Assert.Equal(99, sortKey.StableId);
    }

    [Fact]
    public void BuildSortKey_UsesScreenProjectedModeWhenFrameIsAvailable()
    {
        var component = new DepthSortable2DComponent
        {
            SortMode = DepthSortMode2D.ScreenProjected,
            StableId = 7,
        };
        var frame = new RenderFrame(
            Matrix.Identity,
            Matrix.Identity,
            Vector3.Zero,
            new Rectangle(0, 0, 100, 200));

        var sortKey = component.BuildSortKey(new Vector3(0f, -0.5f, 0f), frame);

        Assert.Equal(15000, sortKey.SortCoordinate);
    }

    [Fact]
    public void Load_ReadsOptionalDepthFields()
    {
        var component = new DepthSortable2DComponent();
        var json = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "depth",
            ["render_pass"] = "Foreground",
            ["sorting_layer"] = "42",
            ["order_in_layer"] = "4",
            ["elevation"] = "2",
            ["sort_anchor"] = new JObject
            {
                ["x"] = 1.5f,
                ["y"] = 2.5f,
            },
            ["local_sort_offset"] = "-1",
            ["sort_mode"] = "TopDownYDown",
            ["stable_id"] = "123",
        };

        component.Load(json);

        Assert.Equal(RenderPass2D.Foreground, component.RenderPass);
        Assert.Equal(42, component.SortingLayer);
        Assert.Equal(4, component.OrderInLayer);
        Assert.Equal(2, component.Elevation);
        Assert.Equal(new Vector2(1.5f, 2.5f), component.SortAnchorLocal);
        Assert.Equal(-1, component.LocalSortOffset);
        Assert.Equal(DepthSortMode2D.TopDownYDown, component.SortMode);
        Assert.Equal(123, component.StableId);
    }
}
