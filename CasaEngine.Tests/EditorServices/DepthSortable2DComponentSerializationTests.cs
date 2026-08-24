using System.Linq;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// Same bug class as <see cref="CharacterControllerComponentSerializationTests"/>:
/// <see cref="DepthSortable2DComponent"/> used to fall into <c>EditorEntityJsonSerializer</c>'s
/// generic <c>default:</c> save branch, which only writes <c>ObjectBase</c> + <c>type</c> - every
/// depth-sort setting was silently dropped on save even though
/// <see cref="DepthSortable2DComponent.Load"/> reads all nine keys back.
/// </summary>
public class DepthSortable2DComponentSerializationTests
{
    [Fact]
    public void SaveEntity_ThenLoad_RoundTripsEveryDepthSortSetting()
    {
        var entity = new Entity
        {
            RootComponent = new TransformComponent(),
        };

        // Every value differs from the property's own default, so a dropped key fails the assertion
        // below rather than passing on a default that happens to match.
        var component = new DepthSortable2DComponent
        {
            RenderPass = RenderPass2D.Foreground,
            SortingLayer = 412,
            OrderInLayer = 7,
            Elevation = 33,
            SortAnchorLocal = new Vector2(2.5f, -4.25f),
            LocalSortOffset = -9,
            SortMode = DepthSortMode2D.IsometricAxis,
            StableId = 987654,
        };

        entity.AddComponent(component);

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);

        var loaded = new Entity();
        loaded.Load(node);

        var loadedComponent = Assert.Single(loaded.Components.OfType<DepthSortable2DComponent>());

        Assert.Equal(RenderPass2D.Foreground, loadedComponent.RenderPass);
        Assert.Equal(412, loadedComponent.SortingLayer);
        Assert.Equal(7, loadedComponent.OrderInLayer);
        Assert.Equal(33, loadedComponent.Elevation);
        Assert.Equal(new Vector2(2.5f, -4.25f), loadedComponent.SortAnchorLocal);
        Assert.Equal(-9, loadedComponent.LocalSortOffset);
        Assert.Equal(DepthSortMode2D.IsometricAxis, loadedComponent.SortMode);
        Assert.Equal(987654, loadedComponent.StableId);
    }
}
