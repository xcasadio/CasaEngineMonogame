using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Covers the per-draw <see cref="SpriteBlendMode"/> added to the sorted sprite path of
/// <see cref="SpriteRendererComponent"/>: the mode is carried through submission on the keyed
/// overloads, defaults to <see cref="SpriteBlendMode.Opaque"/> on every legacy overload, and a
/// key-sorted mixed sequence produces the same run-switch points the draw loop would apply.
///
/// The device-facing draw loop itself cannot run headless (it needs a real GraphicsDevice), so these
/// tests work at the queue/comparator level, exactly like <c>TileMapComponentSortedOverlayTests</c>:
/// they inspect the sorted `_spriteDatas` list (order plus each entry's BlendMode) and reproduce the
/// draw loop's run-detection rule (state changes only when BlendMode differs from the previous entry).
/// </summary>
public class SpriteRendererComponentBlendModeTests
{
    private static readonly Rectangle SourceRectangle = new(0, 0, 16, 16);

    [Fact]
    public void KeyedTextureOverload_WithoutBlendMode_DefaultsToOpaque()
    {
        var component = CreateComponent(out _);
        var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);

        component.DrawSprite(CreateTexture(), SourceRectangle, Point.Zero, Vector2.Zero, 0f,
            Vector2.One, Color.White, 0f, in key, SpriteEffects.None, Rectangle.Empty);

        var entry = GetSpriteDatas(component)[0]!;
        Assert.Equal(SpriteBlendMode.Opaque, (SpriteBlendMode)GetField(entry, "BlendMode"));
    }

    [Fact]
    public void KeyedTextureOverload_WithScissorAndBlendMode_IsCarriedThroughSubmission()
    {
        var component = CreateComponent(out _);
        var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);

        component.DrawSprite(CreateTexture(), SourceRectangle, Point.Zero, Vector2.Zero, 0f,
            Vector2.One, Color.White, 0f, in key, SpriteEffects.None, Rectangle.Empty, SpriteBlendMode.AlphaBlend);

        var entry = GetSpriteDatas(component)[0]!;
        Assert.Equal(SpriteBlendMode.AlphaBlend, (SpriteBlendMode)GetField(entry, "BlendMode"));
    }

    [Fact]
    public void KeyedSpriteOverload_WithExplicitBlendMode_IsCarriedThroughSubmission()
    {
        var component = CreateComponent(out _);
        var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);
        var sprite = CreateSprite();

        component.DrawSprite(sprite, Vector2.Zero, 0f, Vector2.One, Color.White, 0f, in key,
            drawDebug: false, SpriteEffects.None, Rectangle.Empty, SpriteBlendMode.AlphaBlend);

        var entry = GetSpriteDatas(component)[0]!;
        Assert.Equal(SpriteBlendMode.AlphaBlend, (SpriteBlendMode)GetField(entry, "BlendMode"));
    }

    [Theory]
    [InlineData(false)] // unkeyed legacy overload
    [InlineData(true)]  // keyed overload with no blend mode argument
    public void EveryLegacyOverload_DefaultsToOpaque(bool useKeyedOverload)
    {
        var component = CreateComponent(out _);
        var texture = CreateTexture();

        if (useKeyedOverload)
        {
            var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);
            component.DrawSprite(texture, SourceRectangle, Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 0f, in key, SpriteEffects.None, Rectangle.Empty);
        }
        else
        {
            component.DrawSprite(texture, Vector2.Zero, 0f, Vector2.One, Color.White, 0f, SpriteEffects.None, Rectangle.Empty);
        }

        var entry = GetSpriteDatas(component)[0]!;
        Assert.Equal(SpriteBlendMode.Opaque, (SpriteBlendMode)GetField(entry, "BlendMode"));
    }

    [Fact]
    public void MixedKeyOrderedSequence_ProducesExpectedPerRunSwitchPoints()
    {
        var component = CreateComponent(out _);
        var texture = CreateTexture();

        // Five sprites, submitted out of key order, alternating blend mode independently of key order:
        // sort must follow SortKey only, and the resulting per-run switch points must follow the
        // *sorted* BlendMode sequence, not the submission order.
        Submit(component, texture, orderInLayer: 3, SpriteBlendMode.Opaque);
        Submit(component, texture, orderInLayer: 1, SpriteBlendMode.AlphaBlend);
        Submit(component, texture, orderInLayer: 0, SpriteBlendMode.Opaque);
        Submit(component, texture, orderInLayer: 4, SpriteBlendMode.AlphaBlend);
        Submit(component, texture, orderInLayer: 2, SpriteBlendMode.AlphaBlend);

        // Sorting happens inside UpdateBuffer, which Flush() would call before Draw(); invoke it
        // directly so the queue/comparator-level assertions below see the sorted order without
        // requiring a GraphicsDevice.
        InvokeUpdateBuffer(component);

        var spriteDatas = GetSpriteDatas(component);
        Assert.Equal(5, spriteDatas.Count);

        // Expected order by OrderInLayer (0..4) and, from that, the expected blend mode sequence.
        var expectedBlendModes = new[]
        {
            SpriteBlendMode.Opaque,     // orderInLayer 0
            SpriteBlendMode.AlphaBlend, // orderInLayer 1
            SpriteBlendMode.AlphaBlend, // orderInLayer 2
            SpriteBlendMode.Opaque,     // orderInLayer 3
            SpriteBlendMode.AlphaBlend  // orderInLayer 4
        };

        var actualBlendModes = new SpriteBlendMode[spriteDatas.Count];
        for (var i = 0; i < spriteDatas.Count; i++)
        {
            actualBlendModes[i] = (SpriteBlendMode)GetField(spriteDatas[i]!, "BlendMode");
        }

        Assert.Equal(expectedBlendModes, actualBlendModes);

        // Reproduce the draw loop's run-detection rule: a state change happens only when the current
        // sprite's BlendMode differs from the previous one (the very first sprite always applies its
        // mode because the loop starts from the fixed Opaque device state).
        var switchPoints = new List<int>();
        var currentMode = SpriteBlendMode.Opaque;
        for (var i = 0; i < actualBlendModes.Length; i++)
        {
            if (actualBlendModes[i] != currentMode)
            {
                switchPoints.Add(i);
                currentMode = actualBlendModes[i];
            }
        }

        // Opaque(0) -> AlphaBlend(1): switch at 1. AlphaBlend(1)->AlphaBlend(2): no switch.
        // AlphaBlend(2)->Opaque(3): switch at 3. Opaque(3)->AlphaBlend(4): switch at 4.
        Assert.Equal(new[] { 1, 3, 4 }, switchPoints);
    }

    private static void Submit(SpriteRendererComponent component, Texture2D texture, int orderInLayer, SpriteBlendMode blendMode)
    {
        var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, orderInLayer, 0, 0, 0, 0);
        component.DrawSprite(texture, SourceRectangle, Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 0f, in key, SpriteEffects.None, Rectangle.Empty, blendMode);
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    private static Sprite CreateSprite()
    {
        var texture = new CasaEngine.Framework.Assets.Textures.Texture { Resource = CreateTexture() };
        var spriteData = new SpriteData
        {
            PositionInTexture = SourceRectangle,
            Origin = Point.Zero
        };

        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        SetBackingField(sprite, nameof(Sprite.Texture), texture);
        SetBackingField(sprite, nameof(Sprite.SpriteData), spriteData);
        return sprite;
    }

    private static SpriteRendererComponent CreateComponent(out CasaEngineGame game)
    {
        game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBaseField<Game>(game, "_components", new GameComponentCollection());

        // A real SpriteRendererComponent: its constructor only needs game.Components (queuing sprites
        // in DrawSprite never touches the GraphicsDevice, only Flush()/Draw() does).
        return new SpriteRendererComponent(game);
    }

    private static void InvokeUpdateBuffer(SpriteRendererComponent component)
    {
        // UpdateBuffer itself also calls _vertexBuffer.SetData, which needs LoadContent (and therefore
        // a GraphicsDevice) to have run. Sort the queue directly instead, using the exact same
        // comparator UpdateBuffer uses, without requiring a GraphicsDevice.
        var spriteDatas = GetSpriteDatas(component);
        var comparisonField = typeof(SpriteRendererComponent).GetField("SpriteDisplayDataComparison", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(comparisonField);
        var comparison = comparisonField!.GetValue(null);
        var sortMethod = spriteDatas.GetType().GetMethod("Sort", new[] { comparison!.GetType() });
        Assert.NotNull(sortMethod);
        sortMethod!.Invoke(spriteDatas, new[] { comparison });
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(component)!;
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetBaseField<TBase>(object instance, string fieldName, object value)
    {
        var field = typeof(TBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
