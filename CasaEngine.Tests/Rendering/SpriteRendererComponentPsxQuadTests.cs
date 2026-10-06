using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Covers the free PSX quad of the sprite renderer (E19.g G2b-1, ADR-0068): <c>DrawPsxQuad</c> queues the four corners of a
/// PS1 quad as the four vertices of one entry, in the slot order that gives the PS1 triangle split (TR-BL diagonal), with the
/// texture coordinates of the window never flipped (the mirror is in the geometry) and shifted by half a texel plus 1/4096 of a
/// texel (the tie resolved upward). The device-facing draw loop cannot run headless, so these tests work at the queue and
/// vertex-batch level, like <c>SpriteRendererComponentPsxSemiTransparencyTests</c>; the pixels are proved by the engine demo.
/// </summary>
public class SpriteRendererComponentPsxQuadTests
{
    private const int TextureSize = 16;
    private const float TexelShift = 0.5f + 1f / 4096f;

    private static readonly Rectangle Window = new(2, 3, 8, 6);
    private static readonly Color QuadColor = new(200, 100, 50, 255);

    // World coordinates (y up), the four vertices in the order of the PS1 data: TL, TR, BL, BR of the texture rectangle.
    private static readonly Vector2 Tl = new(10f, 20f);
    private static readonly Vector2 Tr = new(30f, 22f);
    private static readonly Vector2 Bl = new(12f, 2f);
    private static readonly Vector2 Br = new(34f, 0f);
    private static readonly Vector2 Center = new(21.5f, 11f);

    [Fact]
    public void Corners_AreWrittenInTheSlotsTrBrBlTl_AsOffsetsFromTheirCentre()
    {
        var component = CreateComponent();

        component.DrawPsxQuad(CreateTexture(), Window, Tl, Tr, Bl, Br, QuadColor, 5f, Key(3),
            SpritePsxSemiTransparency.None, Rectangle.Empty);

        var vertices = FillAndGetVertices(component, 1);
        // Slots 0..3 of the entry: the index buffer {0, 1, 2, 0, 2, 3} then draws (TR, BR, BL) and (TR, BL, TL).
        AssertVertex(vertices[0], Tr - Center);
        AssertVertex(vertices[1], Br - Center);
        AssertVertex(vertices[2], Bl - Center);
        AssertVertex(vertices[3], Tl - Center);
    }

    [Fact]
    public void TextureCoordinates_AreTheWindowCorners_ShiftedByHalfATexelPlusAQuarterThousandth()
    {
        var component = CreateComponent();

        component.DrawPsxQuad(CreateTexture(), Window, Tl, Tr, Bl, Br, QuadColor, 5f, Key(3),
            SpritePsxSemiTransparency.None, Rectangle.Empty);

        var vertices = FillAndGetVertices(component, 1);
        var left = (Window.Left + TexelShift) / TextureSize;
        var right = (Window.Right + TexelShift) / TextureSize;
        var top = (Window.Top + TexelShift) / TextureSize;
        var bottom = (Window.Bottom + TexelShift) / TextureSize;
        Assert.Equal(new Vector2(right, top), vertices[0].TextureCoordinate);    // TR
        Assert.Equal(new Vector2(right, bottom), vertices[1].TextureCoordinate); // BR
        Assert.Equal(new Vector2(left, bottom), vertices[2].TextureCoordinate);  // BL
        Assert.Equal(new Vector2(left, top), vertices[3].TextureCoordinate);     // TL
    }

    [Fact]
    public void MirroredCorners_KeepTheTextureCoordinatesOfTheWindow_NeverFlipped()
    {
        var component = CreateComponent();
        // The same quad mirrored in x: the PS1 data names the left vertices on the right. The window is the raw PS1 window
        // (the producer already stepped it back by one texel on the mirrored axis): the coordinates follow it as given.
        var mirroredWindow = new Rectangle(3, 3, 8, 6);

        component.DrawPsxQuad(CreateTexture(), mirroredWindow, Tr, Tl, Br, Bl, QuadColor, 5f, Key(3),
            SpritePsxSemiTransparency.None, Rectangle.Empty);

        var vertices = FillAndGetVertices(component, 1);
        var left = (mirroredWindow.Left + TexelShift) / TextureSize;
        var right = (mirroredWindow.Right + TexelShift) / TextureSize;
        var top = (mirroredWindow.Top + TexelShift) / TextureSize;
        var bottom = (mirroredWindow.Bottom + TexelShift) / TextureSize;
        // "Top-left" of the data is now at Tr's place: its slot (3) still carries the window's top-left coordinate.
        AssertVertex(vertices[3], Tr - Center);
        Assert.Equal(new Vector2(left, top), vertices[3].TextureCoordinate);
        AssertVertex(vertices[0], Tl - Center);
        Assert.Equal(new Vector2(right, top), vertices[0].TextureCoordinate);
        Assert.Equal(new Vector2(right, bottom), vertices[1].TextureCoordinate);
        Assert.Equal(new Vector2(left, bottom), vertices[2].TextureCoordinate);
    }

    [Fact]
    public void WorldMatrix_IsTheTranslationToTheCentreAndZ_Only()
    {
        var component = CreateComponent();

        component.DrawPsxQuad(CreateTexture(), Window, Tl, Tr, Bl, Br, QuadColor, 5f, Key(3),
            SpritePsxSemiTransparency.None, Rectangle.Empty);

        var entry = GetSpriteDatas(component)[0]!;
        Assert.Equal(Matrix.CreateTranslation(Center.X, Center.Y, 5f), (Matrix)GetField(entry, "WorldMatrix"));
    }

    [Fact]
    public void WithoutMode_IsOneOpaqueEntryOnTheNeutralWindow_WithEveryFieldOfTheQuad()
    {
        var component = CreateComponent();
        var texture = CreateTexture();
        var scissor = new Rectangle(1, 2, 300, 200);
        var key = Key(9);

        component.DrawPsxQuad(texture, Window, Tl, Tr, Bl, Br, QuadColor, 5f, in key, SpritePsxSemiTransparency.None, scissor);

        var entries = GetSpriteDatas(component);
        Assert.Equal(1, entries.Count);
        AssertQuadEntry(entries[0]!, SpriteBlendMode.Opaque, -1f, 2f, QuadColor, texture, scissor, true, key);
    }

    [Theory]
    [InlineData(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.AlphaBlend)]
    [InlineData(SpritePsxSemiTransparency.Mode1, SpriteBlendMode.Additive)]
    [InlineData(SpritePsxSemiTransparency.Mode2, SpriteBlendMode.Subtractive)]
    public void WithMode_IsTwoEntries_SameKeyZAndCorners_OpaqueWindowThenStpWindow(
        SpritePsxSemiTransparency mode, SpriteBlendMode expectedStpBlend)
    {
        var component = CreateComponent();
        var texture = CreateTexture();
        var scissor = new Rectangle(1, 2, 300, 200);
        var key = Key(9);

        component.DrawPsxQuad(texture, Window, Tl, Tr, Bl, Br, QuadColor, 5f, in key, mode, scissor);

        var entries = GetSpriteDatas(component);
        Assert.Equal(2, entries.Count);
        AssertQuadEntry(entries[0]!, SpriteBlendMode.Opaque, 0.75f, 1f, QuadColor, texture, scissor, true, key);
        AssertQuadEntry(entries[1]!, expectedStpBlend, 0.25f, 0.75f, QuadColor, texture, scissor, true, key);
        Assert.Equal(GetField(entries[0]!, "TopLeft"), GetField(entries[1]!, "TopLeft"));
        Assert.Equal(GetField(entries[0]!, "TopRight"), GetField(entries[1]!, "TopRight"));
        Assert.Equal(GetField(entries[0]!, "BottomLeft"), GetField(entries[1]!, "BottomLeft"));
        Assert.Equal(GetField(entries[0]!, "BottomRight"), GetField(entries[1]!, "BottomRight"));
        Assert.Equal(GetField(entries[0]!, "WorldMatrix"), GetField(entries[1]!, "WorldMatrix"));
    }

    [Fact]
    public void Mode3_DrawsTheStpEntryWithTheQuarterColour()
    {
        var component = CreateComponent();
        var key = Key(9);

        component.DrawPsxQuad(CreateTexture(), Window, Tl, Tr, Bl, Br, QuadColor, 5f, in key,
            SpritePsxSemiTransparency.Mode3, Rectangle.Empty);

        var entries = GetSpriteDatas(component);
        Assert.Equal(2, entries.Count);
        Assert.Equal(QuadColor, (Color)GetField(entries[0]!, "Color"));
        Assert.Equal(new Color(64, 64, 64, 255), (Color)GetField(entries[1]!, "Color"));
        Assert.Equal(SpriteBlendMode.Additive, (SpriteBlendMode)GetField(entries[1]!, "BlendMode"));
    }

    [Fact]
    public void ZOrderPath_HasNoSortKey_AndTakesTheZ()
    {
        var component = CreateComponent();
        var scissor = new Rectangle(1, 2, 300, 200);

        component.DrawPsxQuad(CreateTexture(), Window, Tl, Tr, Bl, Br, QuadColor, 7f, SpritePsxSemiTransparency.None, scissor);

        var entries = GetSpriteDatas(component);
        Assert.Equal(1, entries.Count);
        Assert.False((bool)GetField(entries[0]!, "HasSortKey"));
        Assert.True((bool)GetField(entries[0]!, "NoCull"));
        Assert.True((bool)GetField(entries[0]!, "PsxQuad"));
        Assert.Equal(scissor, (Rectangle)GetField(entries[0]!, "ScissorRectangle"));
        Assert.Equal(7f, ((Matrix)GetField(entries[0]!, "WorldMatrix")).Translation.Z);
    }

    [Fact]
    public void ReusedPooledEntry_IsFullyAssigned_WhateverItsPreviousUse()
    {
        var component = CreateComponent();
        var texture = CreateTexture();
        var primeKey = Key(1);
        // Prime the pool with entries that carry a non-default value of every field a quad must overwrite: a depth-ignoring
        // additive sprite on a scissor, then a mode-1 sprite (non-neutral windows).
        component.DrawSprite(texture, new Rectangle(0, 0, 4, 4), Point.Zero, new Vector2(100f, 100f), 0.3f, new Vector2(3f, 3f),
            Color.Red, 9f, in primeKey, SpriteEffects.FlipHorizontally, new Rectangle(5, 6, 7, 8), SpriteBlendMode.Additive, ignoresDepth: true);
        component.DrawSprite(texture, new Rectangle(0, 0, 4, 4), Point.Zero, new Vector2(100f, 100f), 0.3f, new Vector2(3f, 3f),
            Color.Red, 9f, in primeKey, SpriteEffects.None, new Rectangle(5, 6, 7, 8), SpritePsxSemiTransparency.Mode1);
        InvokePrivate(component, "Clear");
        Assert.Empty(GetSpriteDatas(component));

        var scissor = new Rectangle(1, 2, 300, 200);
        var key = Key(9);
        component.DrawPsxQuad(texture, Window, Tl, Tr, Bl, Br, QuadColor, 5f, in key, SpritePsxSemiTransparency.None, scissor);
        component.DrawPsxQuad(texture, Window, Tl, Tr, Bl, Br, QuadColor, 5f, in key, SpritePsxSemiTransparency.Mode0, scissor);

        var entries = GetSpriteDatas(component);
        Assert.Equal(3, entries.Count);
        AssertQuadEntry(entries[0]!, SpriteBlendMode.Opaque, -1f, 2f, QuadColor, texture, scissor, true, key);
        AssertQuadEntry(entries[1]!, SpriteBlendMode.Opaque, 0.75f, 1f, QuadColor, texture, scissor, true, key);
        AssertQuadEntry(entries[2]!, SpriteBlendMode.AlphaBlend, 0.25f, 0.75f, QuadColor, texture, scissor, true, key);
    }

    [Fact]
    public void ReusedQuadEntry_BecomesASpriteEntry_WithoutNoCullNorPsxQuad()
    {
        var component = CreateComponent();
        var texture = CreateTexture();
        var key = Key(9);
        component.DrawPsxQuad(texture, Window, Tl, Tr, Bl, Br, QuadColor, 5f, in key, SpritePsxSemiTransparency.Mode1, Rectangle.Empty);
        InvokePrivate(component, "Clear");

        component.DrawSprite(texture, new Rectangle(0, 0, 4, 4), Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 0f,
            in key, SpriteEffects.None, Rectangle.Empty);
        component.DrawSprite(texture, new Rectangle(0, 0, 4, 4), Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 0f,
            in key, SpriteEffects.None, Rectangle.Empty, SpritePsxSemiTransparency.Mode2);

        var entries = GetSpriteDatas(component);
        Assert.Equal(3, entries.Count);
        foreach (var entry in entries)
        {
            Assert.False((bool)GetField(entry!, "NoCull"));
            Assert.False((bool)GetField(entry!, "PsxQuad"));
        }
    }

    // ---- helpers ----

    private static void AssertQuadEntry(object entry, SpriteBlendMode blend, float alphaMin, float alphaMax, Color color,
        Texture2D texture, Rectangle scissor, bool hasSortKey, RenderSortKey2D key)
    {
        Assert.Equal(blend, (SpriteBlendMode)GetField(entry, "BlendMode"));
        Assert.Equal(alphaMin, (float)GetField(entry, "AlphaMin"));
        Assert.Equal(alphaMax, (float)GetField(entry, "AlphaMax"));
        Assert.Equal(color, (Color)GetField(entry, "Color"));
        Assert.Same(texture, GetField(entry, "Texture"));
        Assert.Equal(scissor, (Rectangle)GetField(entry, "ScissorRectangle"));
        Assert.Equal(hasSortKey, (bool)GetField(entry, "HasSortKey"));
        Assert.Equal(key, (RenderSortKey2D)GetField(entry, "SortKey"));
        Assert.False((bool)GetField(entry, "IgnoresDepth"));
        Assert.True((bool)GetField(entry, "NoCull"));
        Assert.True((bool)GetField(entry, "PsxQuad"));
    }

    private static void AssertVertex(VertexPositionTexture vertex, Vector2 expectedPosition)
    {
        Assert.Equal(new Vector3(expectedPosition, 0f), vertex.Position);
    }

    private static VertexPositionTexture[] FillAndGetVertices(SpriteRendererComponent component, int entryCount)
    {
        var count = component.FillVertices();
        Assert.Equal(entryCount * 4, count);
        var field = typeof(SpriteRendererComponent).GetField("_vertices", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (VertexPositionTexture[])field!.GetValue(component)!;
    }

    private static RenderSortKey2D Key(int orderInLayer)
    {
        return new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, orderInLayer, 0, 0, 0, 0);
    }

    private static Texture2D CreateTexture()
    {
        var texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        SetNonPublicField(texture, typeof(Texture2D), "width", TextureSize);
        SetNonPublicField(texture, typeof(Texture2D), "height", TextureSize);
        return texture;
    }

    private static SpriteRendererComponent CreateComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetNonPublicField(game, typeof(Game), "_components", new GameComponentCollection());
        var component = new SpriteRendererComponent(game);

        // DrawSprite(...) overloads that read the device scissor go through GraphicsDevice: a service whose device is an
        // uninitialized object answers ScissorRectangle with an empty rectangle.
        var service = new FakeGraphicsDeviceService((GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice)));
        var found = false;
        foreach (var owner in new object[] { game, component })
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(IGraphicsDeviceService))
                    {
                        field.SetValue(owner, service);
                        found = true;
                    }
                }
            }
        }

        Assert.True(found);
        return component;
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

    private static void SetNonPublicField(object instance, Type declaringType, string fieldName, object value)
    {
        var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, arguments);
    }

    private sealed class FakeGraphicsDeviceService : IGraphicsDeviceService
    {
        public FakeGraphicsDeviceService(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
        }

        public GraphicsDevice GraphicsDevice { get; }

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? DeviceCreated;
        public event EventHandler<EventArgs>? DeviceDisposing;
        public event EventHandler<EventArgs>? DeviceReset;
        public event EventHandler<EventArgs>? DeviceResetting;
#pragma warning restore CS0067
    }
}
