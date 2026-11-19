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
/// The order of the entries without a sort key at equal z (ADR-0131): the smaller draw order first, then the earlier
/// submission. No GPU: the queue is inspected after <c>FillVertices</c> has sorted it. Every entry carries its identity in
/// the red channel of its colour.
/// </summary>
public class SpriteRendererComponentDrawOrderTests
{
    private const int TextureSize = 16;
    private const int TieCount = 64;

    private static readonly Rectangle Window = new(2, 3, 8, 6);

    // ---- Equal z and equal draw order: the submission order is kept, on both paths ----

    [Fact]
    public void EqualZAndDrawOrder_KeepTheSubmissionOrder_OnTheSpritePath_With64Entries()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        for (var i = 0; i < TieCount; i++)
        {
            DrawTexture(component, texture, i, 5f, drawOrder: 0);
        }

        component.FillVertices();

        AssertIds(component, Sequence(TieCount));
    }

    [Fact]
    public void EqualZAndDrawOrder_KeepTheSubmissionOrder_OnThePsxQuadPath_With64Entries()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        for (var i = 0; i < TieCount; i++)
        {
            DrawQuad(component, texture, i, 5f);
        }

        component.FillVertices();

        AssertIds(component, Sequence(TieCount));
    }

    [Fact]
    public void EqualZAndDrawOrder_KeepTheSubmissionOrder_WhenBothPathsAreInterleaved()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        for (var i = 0; i < TieCount; i++)
        {
            if (i % 2 == 0)
            {
                DrawTexture(component, texture, i, 5f, drawOrder: 0);
            }
            else
            {
                DrawQuad(component, texture, i, 5f);
            }
        }

        component.FillVertices();

        AssertIds(component, Sequence(TieCount));
    }

    [Fact]
    public void Submission_IsTheRankOfTheEntryInTheQueue_OnBothPaths_AndRestartsAfterAClear()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        DrawTexture(component, texture, 0, 5f, drawOrder: 0);
        DrawQuad(component, texture, 1, 5f);
        DrawTexture(component, texture, 2, 5f, drawOrder: 3);
        AssertSubmissions(component, 0, 1, 2);

        InvokePrivate(component, "Clear");
        DrawQuad(component, texture, 3, 5f);
        DrawTexture(component, texture, 4, 5f, drawOrder: 0);

        AssertSubmissions(component, 0, 1);
    }

    [Fact]
    public void PsxQuadWithAMode_QueuesTwoEntries_OfTwoConsecutiveSubmissions()
    {
        var component = CreateComponent();

        component.DrawPsxQuad(CreateTexture(), Window, new Vector2(0, 4), new Vector2(4, 4), new Vector2(0, 0), new Vector2(4, 0),
            new Color(1, 0, 0, 255), 5f, SpritePsxSemiTransparency.Mode0, Rectangle.Empty);

        AssertSubmissions(component, 0, 1);
    }

    // ---- The draw order ----

    [Fact]
    public void EqualZ_DifferentDrawOrders_GiveTheIncreasingOrder_WhateverTheSubmissionOrder()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        // The ids are the draw orders; submitted in a scrambled order, on both paths.
        int[] submitted = [5, 2, 9, 0, 7, 3, 8, 1, 6, 4];
        for (var i = 0; i < submitted.Length; i++)
        {
            if (i % 2 == 0)
            {
                DrawTexture(component, texture, submitted[i], 5f, drawOrder: submitted[i]);
            }
            else
            {
                // A quad carries no draw order: it joins the entries of draw order 0, behind them by submission.
                DrawQuad(component, texture, 100 + submitted[i], 5f);
            }
        }

        component.FillVertices();

        // The five quads (draw order 0) come first in their submission order, then the five sprites by increasing draw order.
        AssertIds(component, [102, 100, 103, 101, 104, 5, 6, 7, 8, 9]);
    }

    [Fact]
    public void EqualZ_NegativeAndPositiveDrawOrders_AreOrderedAroundZero()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        DrawTexture(component, texture, 1, 5f, drawOrder: 4);
        DrawTexture(component, texture, 2, 5f, drawOrder: -4);
        DrawTexture(component, texture, 3, 5f, drawOrder: 0);

        component.FillVertices();

        AssertIds(component, [2, 3, 1]);
    }

    [Fact]
    public void DifferentZ_KeepTheLargerZFirst_WhateverTheDrawOrder()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        DrawTexture(component, texture, 1, 1f, drawOrder: -10);
        DrawTexture(component, texture, 2, 3f, drawOrder: 10);
        DrawTexture(component, texture, 3, 2f, drawOrder: 0);

        component.FillVertices();

        AssertIds(component, [2, 3, 1]);
    }

    [Fact]
    public void DrawOrder_OfTheSpriteOverloads_ReachesTheEntry()
    {
        var component = CreateComponent();
        var sprite = CreateSprite();

        component.DrawSprite(sprite, Vector2.Zero, 0f, Vector2.One, Color.White, 5f, false, SpriteEffects.None, Rectangle.Empty, 11);
        component.DrawSprite(sprite, Vector2.Zero, 0f, Vector2.One, Color.White, 5f, SpriteBlendMode.AlphaBlend, 12);
        component.DrawSprite(sprite, Vector2.Zero, 0f, Vector2.One, Color.White, 5f, SpriteBlendMode.AlphaBlend, Rectangle.Empty, 13);
        component.DrawSprite(sprite, Vector2.Zero, 0f, Vector2.One, Color.White, 5f, false, SpriteEffects.None, Rectangle.Empty);
        component.DrawSprite(sprite, Vector2.Zero, 0f, Vector2.One, Color.White, 5f, SpriteBlendMode.AlphaBlend);
        component.DrawSprite(CreateTexture(), Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 5f, SpriteEffects.None, true, 14);

        var entries = GetSpriteDatas(component);
        Assert.Equal(6, entries.Count);
        Assert.Equal(11, (int)GetField(entries[0]!, "DrawOrder"));
        Assert.Equal(12, (int)GetField(entries[1]!, "DrawOrder"));
        Assert.Equal(13, (int)GetField(entries[2]!, "DrawOrder"));
        Assert.Equal(0, (int)GetField(entries[3]!, "DrawOrder"));
        Assert.Equal(0, (int)GetField(entries[4]!, "DrawOrder"));
        Assert.Equal(14, (int)GetField(entries[5]!, "DrawOrder"));
        Assert.True((bool)GetField(entries[5]!, "IgnoresDepth"));
        for (var i = 0; i < 6; i++)
        {
            Assert.False((bool)GetField(entries[i]!, "HasSortKey"));
        }
    }

    // ---- Pool reuse ----

    [Fact]
    public void ReusedEntries_NeverKeepTheDrawOrderOfAPreviousFrame()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        // Two stale entries in the pool, both with a non-zero draw order.
        DrawTexture(component, texture, 1, 5f, drawOrder: 7);
        DrawTexture(component, texture, 2, 5f, drawOrder: 9);
        component.FillVertices();
        InvokePrivate(component, "Clear");
        Assert.Empty(GetSpriteDatas(component));

        // The entries the core queued carry its own assignment, so a missing assignment there would leave nothing stale to
        // see. Poison the pool itself: every pooled entry now holds a draw order and a rank no call would write.
        PoisonPool(component);

        // One entry per path, each reusing a stale entry.
        DrawQuad(component, texture, 3, 5f);
        component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, new Color(4, 0, 0, 255), 5f, SpriteEffects.None, false);

        var entries = GetSpriteDatas(component);
        Assert.Equal(2, entries.Count);
        Assert.Equal(0, (int)GetField(entries[0]!, "DrawOrder"));
        Assert.Equal(0, (int)GetField(entries[1]!, "DrawOrder"));
        Assert.Equal(0, (int)GetField(entries[0]!, "Submission"));
        Assert.Equal(1, (int)GetField(entries[1]!, "Submission"));
    }

    // ---- Keyed entries ----

    [Fact]
    public void KeyedEntries_KeepTheirComparison_AndIgnoreTheDrawOrder()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, new Color(1, 0, 0, 255), 5f, Key(30), SpriteEffects.None);
        component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, new Color(2, 0, 0, 255), 5f, Key(10), SpriteEffects.None);
        component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, new Color(3, 0, 0, 255), 5f, Key(20), SpriteEffects.None);

        component.FillVertices();

        AssertIds(component, [2, 3, 1]);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(5)]
    public void KeyedAgainstUnkeyed_FollowsTheKeys_WhateverTheDrawOrderOfTheUnkeyedEntry(int drawOrder)
    {
        var component = CreateComponent();
        var texture = CreateTexture();
        var keyed = Key(10);

        component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, new Color(1, 0, 0, 255), 5f, keyed, SpriteEffects.None);
        DrawTexture(component, texture, 2, 5f, drawOrder);

        component.FillVertices();

        var keyFirst = keyed.CompareTo(RenderSortKey2D.Default) < 0;
        AssertIds(component, keyFirst ? [1, 2] : [2, 1]);
    }

    // ---- No allocation ----

    [Fact]
    public void QueueingAndSorting_AllocateNothing_OnceThePoolIsWarm()
    {
        var component = CreateComponent();
        var texture = CreateTexture();

        // Warm-up: fills the pool and sizes the vertex array.
        for (var i = 0; i < TieCount; i++)
        {
            DrawTexture(component, texture, i, 5f, drawOrder: i % 3);
            DrawQuad(component, texture, i, 5f);
        }

        component.FillVertices();
        InvokePrivate(component, "Clear");
        var scissor = Rectangle.Empty;
        var color = Color.White;

        var before = AllocationWindow.Start();
        for (var i = 0; i < TieCount; i++)
        {
            component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, color, 5f, SpriteEffects.None, false, i % 3);
            component.DrawPsxQuad(texture, Window, Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One, color, 5f,
                SpritePsxSemiTransparency.None, scissor);
        }

        component.FillVertices();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    // ---- Helpers ----

    private static void DrawTexture(SpriteRendererComponent component, Texture2D texture, int id, float z, int drawOrder)
    {
        component.DrawSprite(texture, Window, Point.Zero, Vector2.Zero, 0f, Vector2.One, new Color(id, 0, 0, 255), z,
            SpriteEffects.None, false, drawOrder);
    }

    private static void DrawQuad(SpriteRendererComponent component, Texture2D texture, int id, float z)
    {
        component.DrawPsxQuad(texture, Window, new Vector2(0, 4), new Vector2(4, 4), new Vector2(0, 0), new Vector2(4, 0),
            new Color(id, 0, 0, 255), z, SpritePsxSemiTransparency.None, Rectangle.Empty);
    }

    private static int[] Sequence(int count)
    {
        var ids = new int[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = i;
        }

        return ids;
    }

    private static void AssertIds(SpriteRendererComponent component, int[] expected)
    {
        var entries = GetSpriteDatas(component);
        var actual = new int[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            actual[i] = ((Color)GetField(entries[i]!, "Color")).R;
        }

        Assert.Equal(expected, actual);
    }

    private static void AssertSubmissions(SpriteRendererComponent component, params int[] expected)
    {
        var entries = GetSpriteDatas(component);
        var actual = new int[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            actual[i] = (int)GetField(entries[i]!, "Submission");
        }

        Assert.Equal(expected, actual);
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

    private static Sprite CreateSprite()
    {
        var sheet = new CasaEngine.Framework.Assets.Textures.Texture { Resource = CreateTexture() };
        var spriteData = new SpriteData { PositionInTexture = Window, Origin = Point.Zero };

        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        SetBackingField(sprite, nameof(Sprite.Texture), sheet);
        SetBackingField(sprite, nameof(Sprite.SpriteData), spriteData);
        return sprite;
    }

    private static SpriteRendererComponent CreateComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetNonPublicField(game, typeof(Game), "_components", new GameComponentCollection());
        var component = new SpriteRendererComponent(game);

        // DrawSprite overloads that read the device scissor go through GraphicsDevice: a service whose device is an
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

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void PoisonPool(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_freeSpriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var pool = field!.GetValue(component)!;
        var poolType = pool.GetType();
        var pop = poolType.GetMethod("Pop")!;
        var push = poolType.GetMethod("Push")!;
        var count = (int)poolType.GetProperty("Count")!.GetValue(pool)!;
        Assert.True(count >= 2);

        var entries = new object[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] = pop.Invoke(pool, null)!;
            entries[i].GetType().GetField("DrawOrder")!.SetValue(entries[i], 77);
            entries[i].GetType().GetField("Submission")!.SetValue(entries[i], 99);
        }

        for (var i = 0; i < count; i++)
        {
            push.Invoke(pool, [entries[i]]);
        }
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, null);
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
