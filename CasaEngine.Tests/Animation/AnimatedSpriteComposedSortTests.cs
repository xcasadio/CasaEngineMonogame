using System.Reflection;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Covers the sort key <see cref="AnimatedSpriteComponent.DrawComposedAnimation"/> submits for each part
/// of a composed animation: every part must share the entity's own sort coordinate (built once from the
/// component's world position, never from a per-part position), with <c>DrawOrder</c> (via
/// <c>BuildPartSortKey</c>'s <c>LocalSortOffset</c>) arbitrating order within the entity.
/// </summary>
public class AnimatedSpriteComposedSortTests
{
    [Fact]
    public void ComposedParts_ShareTheEntitySortCoordinate_AndOrderByDrawOrder()
    {
        // Part A sits higher on screen (a smaller world Y, offset +100 from the entity) but has the
        // higher DrawOrder (2, meant to draw last / in front). Part B sits lower on screen (offset -100)
        // but has the lower DrawOrder (1, meant to draw first / behind). The vertical offsets are the
        // exact opposite of the DrawOrder ranking: a per-part-position sort key would flip the order
        // that DrawOrder demands (see the previous bug); an entity-anchored sort key must not.
        var animationData = new Animation2dData();
        animationData.Parts.Add(new Animation2dPartData
        {
            Id = "torso",
            DefaultPosition = new Vector2(0f, 100f),
            DefaultDrawOrder = 2,
        });
        animationData.Parts.Add(new Animation2dPartData
        {
            Id = "legs",
            DefaultPosition = new Vector2(0f, -100f),
            DefaultDrawOrder = 1,
        });

        var component = new AnimatedSpriteComponent();
        component.LocalPosition = new Vector3(0f, 0f, 0f);
        component.AddAnimation(new CasaEngine.Framework.Assets.Animations.Animation2d(animationData));
        component.SetCurrentAnimation(0, true);

        var runtimeState = GetSampler(component).RuntimeState;
        Assert.Equal(2, runtimeState.PartCount);
        var torsoPart = runtimeState.GetPart(0);
        var legsPart = runtimeState.GetPart(1);
        Assert.Equal(2, torsoPart.DrawOrder);
        Assert.Equal(1, legsPart.DrawOrder);

        var depthSortable = new DepthSortable2DComponent
        {
            SortMode = DepthSortMode2D.TopDownYUp,
            LocalSortOffset = 0,
        };

        // Mirrors AnimatedSpriteComponent.DrawComposedAnimation: the base key is built ONCE from the
        // component's own world position, then BuildPartSortKey derives each part's key from it.
        var baseSortKey = depthSortable.BuildSortKey(component.Position, null);
        var buildPartSortKey = typeof(AnimatedSpriteComponent).GetMethod(
            "BuildPartSortKey", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(buildPartSortKey);

        var torsoKey = (RenderSortKey2D)buildPartSortKey!.Invoke(null, new object[] { baseSortKey, torsoPart })!;
        var legsKey = (RenderSortKey2D)buildPartSortKey.Invoke(null, new object[] { baseSortKey, legsPart })!;

        // Same entity coordinate for both parts: the (very different) per-part Y offsets never leak
        // into SortCoordinate.
        Assert.Equal(baseSortKey.SortCoordinate, torsoKey.SortCoordinate);
        Assert.Equal(baseSortKey.SortCoordinate, legsKey.SortCoordinate);
        Assert.Equal(torsoKey.SortCoordinate, legsKey.SortCoordinate);

        // DrawOrder alone arbitrates: legs (DrawOrder 1) sorts before torso (DrawOrder 2), even though
        // legs sits lower on screen than torso.
        Assert.True(legsKey.CompareTo(torsoKey) < 0);
        Assert.True(torsoKey.CompareTo(legsKey) > 0);
    }

    private static Animation2dCompositionSampler GetSampler(AnimatedSpriteComponent component)
    {
        var field = typeof(AnimatedSpriteComponent).GetField("_currentCompositionSampler", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Animation2dCompositionSampler)field!.GetValue(component)!;
    }
}
