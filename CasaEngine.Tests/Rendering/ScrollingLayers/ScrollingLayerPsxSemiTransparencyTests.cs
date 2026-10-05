using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScrollingLayers;

/// <summary>
/// A scrolling layer that carries a PSX semi-transparency mode draws each covering quad as two entries of the same sort key
/// on two disjoint raw-alpha windows (E19.g G2c, ADR-0051 extended to the background layers); a layer without a mode is
/// unchanged (one entry, the blend of the layer). Queue-level tests, like the sibling submission tests: the pixels are proved
/// by the engine demo.
/// </summary>
public class ScrollingLayerPsxSemiTransparencyTests
{
    private static readonly ScrollingLayerConfiguration Configuration = new(640, 480, 320, 240);
    private static readonly Rectangle Scissor = new(0, 0, 320, 240);

    [Fact]
    public void LayerWithoutMode_IsOneEntry_WithTheBlendOfTheLayer_OnTheNeutralWindow()
    {
        var entries = Submit(MakeLayer(SpritePsxSemiTransparency.None, SpriteBlendMode.Additive));

        var entry = Assert.Single(entries);
        AssertEntry(entry!, SpriteBlendMode.Additive, -1f, 2f, Color.White);
    }

    [Theory]
    [InlineData(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.AlphaBlend, false)]
    [InlineData(SpritePsxSemiTransparency.Mode1, SpriteBlendMode.Additive, false)]
    [InlineData(SpritePsxSemiTransparency.Mode2, SpriteBlendMode.Subtractive, false)]
    [InlineData(SpritePsxSemiTransparency.Mode3, SpriteBlendMode.Additive, true)]
    public void LayerWithMode_IsTwoEntriesOfTheSameKey_OpaqueWindowThenStpWindow(
        SpritePsxSemiTransparency mode, SpriteBlendMode expectedStpBlend, bool expectedQuarterColor)
    {
        var entries = Submit(MakeLayer(mode, SpriteBlendMode.Opaque));

        Assert.Equal(2, entries.Count);
        Assert.Equal(GetField(entries[0]!, "SortKey"), GetField(entries[1]!, "SortKey"));
        Assert.Equal(GetField(entries[0]!, "WorldMatrix"), GetField(entries[1]!, "WorldMatrix"));
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, 0.75f, 1f, Color.White);
        AssertEntry(entries[1]!, expectedStpBlend, 0.25f, 0.75f,
            expectedQuarterColor ? new Color(64, 64, 64, 255) : Color.White);
    }

    [Fact]
    public void LayerWithMode_IgnoresItsBlend()
    {
        var entries = Submit(MakeLayer(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.Additive));

        Assert.Equal(2, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, 0.75f, 1f, Color.White);
        AssertEntry(entries[1]!, SpriteBlendMode.AlphaBlend, 0.25f, 0.75f, Color.White);
    }

    // ---- E19.g G2d: the tint overlay carries the PSX mode of the map (BGColorA) ----

    [Fact]
    public void TintWithMode1_IsOneAdditiveEntry_WithTheColorAsIs()
    {
        var entries = SubmitTint(new ScrollingTintDefinition(new Color(50, 0, 0, 255), TintKey, SpritePsxSemiTransparency.Mode1));

        var entry = Assert.Single(entries);
        AssertEntry(entry!, SpriteBlendMode.Additive, -1f, 2f, new Color(50, 0, 0, 255));
    }

    [Fact]
    public void TintWithMode0_IsOneAlphaBlendEntry_WithAnAlphaOf128()
    {
        var entries = SubmitTint(new ScrollingTintDefinition(new Color(40, 40, 40, 255), TintKey, SpritePsxSemiTransparency.Mode0));

        var entry = Assert.Single(entries);
        AssertEntry(entry!, SpriteBlendMode.AlphaBlend, -1f, 2f, new Color(40, 40, 40, 128));
    }

    [Fact]
    public void TintWithMode2_IsOneSubtractiveEntry_WithTheColorAsIs()
    {
        var entries = SubmitTint(new ScrollingTintDefinition(new Color(50, 0, 0, 255), TintKey, SpritePsxSemiTransparency.Mode2));

        var entry = Assert.Single(entries);
        AssertEntry(entry!, SpriteBlendMode.Subtractive, -1f, 2f, new Color(50, 0, 0, 255));
    }

    [Fact]
    public void TintWithMode3_IsOneAdditiveEntry_WithEachChannelScaledBy64Over255()
    {
        var entries = SubmitTint(new ScrollingTintDefinition(new Color(50, 0, 0, 255), TintKey, SpritePsxSemiTransparency.Mode3));

        var entry = Assert.Single(entries);
        AssertEntry(entry!, SpriteBlendMode.Additive, -1f, 2f, new Color(13, 0, 0, 255));
    }

    [Fact]
    public void TintWithTheTwoArgumentConstructor_IsTodaysEntry_AlphaBlendWithTheColorAsIs()
    {
        var entries = SubmitTint(new ScrollingTintDefinition(new Color(40, 40, 40, 128), TintKey));

        var entry = Assert.Single(entries);
        AssertEntry(entry!, SpriteBlendMode.AlphaBlend, -1f, 2f, new Color(40, 40, 40, 128));
    }

    [Fact]
    public void TintWithAMode_KeepsItsSortKey()
    {
        var entries = SubmitTint(new ScrollingTintDefinition(new Color(50, 0, 0, 255), TintKey, SpritePsxSemiTransparency.Mode1));

        var sortKey = (RenderSortKey2D)GetField(entries[0]!, "SortKey");
        Assert.Equal(TintKey, sortKey);
    }

    // ---- helpers ----

    private static readonly RenderSortKey2D TintKey = new((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);

    private static System.Collections.IList SubmitTint(ScrollingTintDefinition tint)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        var renderer = new SpriteRendererComponent(game);
        var component = new ScrollingLayerComponent(game);

        var whiteField = typeof(ScrollingLayerComponent).GetField("_whiteTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(whiteField);
        whiteField!.SetValue(component, (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)));

        component.Service.SetConfiguration(Configuration);
        component.Service.SetTint(tint);
        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(renderer)!;
    }

    private static ScrollingLayerDefinition MakeLayer(SpritePsxSemiTransparency mode, SpriteBlendMode blend)
    {
        return new ScrollingLayerDefinition
        {
            FrameTextureAssetIds = new[] { Guid.NewGuid() },
            FactorXNum = 0,
            FactorXDenom = 1,
            FactorYNum = 0,
            FactorYDenom = 1,
            AnimTimer = 100,
            Pass = RenderPass2D.Background,
            Blend = blend,
            Tint = Color.White,
            PsxSemiTransparency = mode,
        };
    }

    private static System.Collections.IList Submit(ScrollingLayerDefinition layer)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        var renderer = new SpriteRendererComponent(game);
        var component = new ScrollingLayerComponent(game);

        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { layer });
        component.ResolveTextures(_ => (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)));
        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(renderer)!;
    }

    private static void AssertEntry(object entry, SpriteBlendMode blend, float alphaMin, float alphaMax, Color color)
    {
        Assert.Equal(blend, (SpriteBlendMode)GetField(entry, "BlendMode"));
        Assert.Equal(alphaMin, (float)GetField(entry, "AlphaMin"));
        Assert.Equal(alphaMax, (float)GetField(entry, "AlphaMax"));
        Assert.Equal(color, (Color)GetField(entry, "Color"));
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }
}
