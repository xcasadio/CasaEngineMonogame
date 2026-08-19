using System.Reflection;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Covers the sort key <see cref="AnimatedSpriteComponent.DrawComposedAnimation"/> submits for each part
/// of a composed animation. The test calls the exact production methods
/// <see cref="AnimatedSpriteComponent.BuildComposedAnimationBaseSortKey"/> and
/// <see cref="AnimatedSpriteComponent.BuildPartSortKey"/> that <c>DrawComposedAnimation</c> itself calls
/// (both <c>internal</c>, reachable from this assembly via <c>InternalsVisibleTo</c>) — it does not
/// re-implement the key derivation, so it fails if that production logic regresses.
/// </summary>
public class AnimatedSpriteComposedSortTests
{
    [Fact]
    public void ComposedParts_ShareTheEntitySortCoordinate_AndOrderByDrawOrder()
    {
        // Part A sits higher on screen (a smaller world Y, offset +100 from the entity) but has the
        // higher DrawOrder (2, meant to draw last / in front). Part B sits lower on screen (offset -100)
        // but has the lower DrawOrder (1, meant to draw first / behind). The vertical offsets are the
        // exact opposite of the DrawOrder ranking: before the fix, DrawComposedAnimation built each
        // part's key from ITS OWN world position (entity position + part offset), so the two parts got
        // different SortCoordinate values (-Y under TopDownYUp) that outrank LocalSortOffset in
        // RenderSortKey2D.CompareTo — torso (Y=+100 -> SortCoordinate=-10000) would have sorted BEFORE
        // legs (Y=-100 -> SortCoordinate=+10000), inverting the DrawOrder-mandated order and failing
        // both assertions below. Now that DrawComposedAnimation builds the base key once from the
        // component's own Position via BuildComposedAnimationBaseSortKey (no part input), both parts
        // get the identical SortCoordinate and DrawOrder alone decides the order.
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

        // A minimal Entity/World scaffold: BuildComposedAnimationBaseSortKey reads Owner.World.CurrentRenderFrame,
        // so the component needs a real Owner/World (no GraphicsDevice/Game required for that).
        var world = new CasaEngine.Framework.Scene.World.World();
        var entityRoot = new TestSceneComponent();
        var entity = new Entity { RootComponent = entityRoot };
        SetProperty(entity, nameof(Entity.World), world);

        var component = new AnimatedSpriteComponent();
        component.LocalPosition = new Vector3(0f, 0f, 0f);
        entityRoot.AddChildComponent(component);
        SetPrivateField(component, "_depthSortable2DComponent", new DepthSortable2DComponent
        {
            SortMode = DepthSortMode2D.TopDownYUp,
            LocalSortOffset = 0,
        });

        component.AddAnimation(new CasaEngine.Framework.Assets.Animations.Animation2d(animationData));
        component.SetCurrentAnimation(0, true);

        var runtimeState = GetSampler(component).RuntimeState;
        Assert.Equal(2, runtimeState.PartCount);
        var torsoPart = runtimeState.GetPart(0);
        var legsPart = runtimeState.GetPart(1);
        Assert.Equal(2, torsoPart.DrawOrder);
        Assert.Equal(1, legsPart.DrawOrder);

        // The exact production call chain DrawComposedAnimation executes: one base key per draw call,
        // then one derived key per part.
        var baseSortKey = component.BuildComposedAnimationBaseSortKey();
        var torsoKey = AnimatedSpriteComponent.BuildPartSortKey(baseSortKey, torsoPart);
        var legsKey = AnimatedSpriteComponent.BuildPartSortKey(baseSortKey, legsPart);

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

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other) : base(other)
        {
        }

        public override TestSceneComponent Clone() => new(this);
    }
}
