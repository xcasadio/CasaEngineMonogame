using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Covers the PSX semi-transparency of sprites (E19.g G2a, ADR-0051): a sprite that carries a mode other than
/// <see cref="SpritePsxSemiTransparency.None"/> is queued as two entries of the same sort key, one per raw-alpha window
/// (opaque texels, then STP texels), the second with the blend state of the mode. The device-facing draw loop cannot run
/// headless, so these tests work at the queue level, like <c>SpriteRendererComponentBlendModeTests</c>, and read the
/// window each draw path poses through the <c>AlphaWindowWriter</c> seam. The pixels are proved by the engine demos.
/// </summary>
public class SpriteRendererComponentPsxSemiTransparencyTests
{
    private static readonly Rectangle SourceRectangle = new(0, 0, 16, 16);
    private static readonly Color ComponentColor = new(200, 100, 50, 255);

    [Fact]
    public void SpriteWithoutMode_IsOneEntry_OnTheNeutralWindow_WithTheOpaqueState()
    {
        var component = CreateComponent(out _);
        var key = Key(0);

        component.DrawSprite(CreateSprite(), Vector2.Zero, 0f, Vector2.One, ComponentColor, 0f, in key,
            drawDebug: false, SpriteEffects.None, Rectangle.Empty, SpritePsxSemiTransparency.None);

        var entries = GetSpriteDatas(component);
        Assert.Equal(1, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, -1f, 2f, ComponentColor);
    }

    [Theory]
    [InlineData(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.AlphaBlend, false)]
    [InlineData(SpritePsxSemiTransparency.Mode1, SpriteBlendMode.Additive, false)]
    [InlineData(SpritePsxSemiTransparency.Mode2, SpriteBlendMode.Subtractive, false)]
    [InlineData(SpritePsxSemiTransparency.Mode3, SpriteBlendMode.Additive, true)]
    public void SpriteWithMode_IsTwoEntriesOfTheSameKey_OpaqueWindowThenStpWindow(
        SpritePsxSemiTransparency mode, SpriteBlendMode expectedStpBlend, bool expectedQuarterColor)
    {
        var component = CreateComponent(out _);
        var key = Key(7);

        component.DrawSprite(CreateSprite(), Vector2.Zero, 0f, Vector2.One, ComponentColor, 0f, in key,
            drawDebug: false, SpriteEffects.None, Rectangle.Empty, mode);

        var entries = GetSpriteDatas(component);
        Assert.Equal(2, entries.Count);
        Assert.Equal(GetField(entries[0]!, "SortKey"), GetField(entries[1]!, "SortKey"));
        Assert.True((bool)GetField(entries[0]!, "HasSortKey"));
        Assert.True((bool)GetField(entries[1]!, "HasSortKey"));

        // Before the sort the queue is in insertion order; after it nothing is claimed about the order of the two.
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, 0.75f, 1f, ComponentColor);
        AssertEntry(entries[1]!, expectedStpBlend, 0.25f, 0.75f,
            expectedQuarterColor ? new Color(64, 64, 64, 255) : ComponentColor);
    }

    [Fact]
    public void CallerBlendModeAlone_NeverDoublesTheEntry()
    {
        var component = CreateComponent(out _);
        var key = Key(0);

        component.DrawSprite(CreateTexture(), SourceRectangle, Point.Zero, Vector2.Zero, 0f,
            Vector2.One, Color.White, 0f, in key, SpriteEffects.None, Rectangle.Empty, SpriteBlendMode.Additive);

        var entries = GetSpriteDatas(component);
        Assert.Equal(1, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Additive, -1f, 2f, Color.White);
    }

    [Fact]
    public void ReusedPooledEntry_DoesNotKeepTheWindowOfItsPreviousUse()
    {
        var component = CreateComponent(out _);
        var key = Key(0);
        component.DrawSprite(CreateSprite(), Vector2.Zero, 0f, Vector2.One, ComponentColor, 0f, in key,
            drawDebug: false, SpriteEffects.None, Rectangle.Empty, SpritePsxSemiTransparency.Mode1);
        InvokePrivate(component, "Clear");
        Assert.Empty(GetSpriteDatas(component));

        // Both queued entries came back to the pool with their STP window. A legacy submission pops one of them.
        component.DrawSprite(CreateTexture(), SourceRectangle, Point.Zero, Vector2.Zero, 0f,
            Vector2.One, Color.White, 0f, in key, SpriteEffects.None, Rectangle.Empty);

        var entries = GetSpriteDatas(component);
        Assert.Equal(1, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, -1f, 2f, Color.White);
    }

    [Fact]
    public void DrawDirectly_PosesTheNeutralWindow_BeforeTouchingTheDevice()
    {
        var component = CreateComponent(out _);
        var poses = InstallRecorder(component);
        SetField(component, "_effect", RuntimeHelpers.GetUninitializedObject(typeof(Effect)));

        CallDeviceFacing(() => component.DrawDirectly(CreateTexture()));

        Assert.Equal(new[] { (-1f, 2f) }, poses);
    }

    [Fact]
    public void DrawStaticBatch_PosesTheNeutralWindow_BeforeTouchingTheDevice()
    {
        var component = CreateComponent(out _);
        var poses = InstallRecorder(component);
        SetField(component, "_effect", RuntimeHelpers.GetUninitializedObject(typeof(Effect)));
        var vertexBuffer = (VertexBuffer)RuntimeHelpers.GetUninitializedObject(typeof(VertexBuffer));
        var indexBuffer = (IndexBuffer)RuntimeHelpers.GetUninitializedObject(typeof(IndexBuffer));
        var frame = default(RenderFrame);

        CallDeviceFacing(() => component.DrawStaticBatch(CreateTexture(), vertexBuffer, indexBuffer, 2, Matrix.Identity, in frame));

        Assert.Equal(new[] { (-1f, 2f) }, poses);
    }

    [Fact]
    public void SortedDraw_PosesTheNeutralWindow_BeforeTouchingTheDevice()
    {
        var component = CreateComponent(out _);
        var poses = InstallRecorder(component);
        SetField(component, "_effect", RuntimeHelpers.GetUninitializedObject(typeof(Effect)));

        CallDeviceFacing(() => InvokePrivate(component, "Draw", Matrix.Identity, Matrix.Identity));

        Assert.Equal(new[] { (-1f, 2f) }, poses);
    }

    // ---- AnimatedSpriteComponent: the sorted path passes the mode of each part, the zOrder path stays opaque ----

    [Fact]
    public void AnimatedSprite_WithDepthSortable_SubmitsTheModeOfItsPart_AsTwoEntries()
    {
        var component = CreateAnimatedSprite(SpritePsxSemiTransparency.Mode2, withDepthSortable: true, out var renderer);

        component.Draw(0.016f);

        var entries = GetSpriteDatas(renderer);
        Assert.Equal(2, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, 0.75f, 1f, Color.White);
        AssertEntry(entries[1]!, SpriteBlendMode.Subtractive, 0.25f, 0.75f, Color.White);
    }

    [Fact]
    public void AnimatedSprite_WithoutMode_SubmitsOneEntryOnTheNeutralWindow()
    {
        var component = CreateAnimatedSprite(SpritePsxSemiTransparency.None, withDepthSortable: true, out var renderer);

        component.Draw(0.016f);

        var entries = GetSpriteDatas(renderer);
        Assert.Equal(1, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, -1f, 2f, Color.White);
    }

    [Fact]
    public void AnimatedSprite_WithoutDepthSortable_StaysOneOpaqueEntry_WhateverTheMode()
    {
        var component = CreateAnimatedSprite(SpritePsxSemiTransparency.Mode2, withDepthSortable: false, out var renderer);

        component.Draw(0.016f);

        var entries = GetSpriteDatas(renderer);
        Assert.Equal(1, entries.Count);
        AssertEntry(entries[0]!, SpriteBlendMode.Opaque, -1f, 2f, Color.White);
    }

    // ---- helpers ----

    private static void AssertEntry(object entry, SpriteBlendMode blend, float alphaMin, float alphaMax, Color color)
    {
        Assert.Equal(blend, (SpriteBlendMode)GetField(entry, "BlendMode"));
        Assert.Equal(alphaMin, (float)GetField(entry, "AlphaMin"));
        Assert.Equal(alphaMax, (float)GetField(entry, "AlphaMax"));
        Assert.Equal(color, (Color)GetField(entry, "Color"));
    }

    private static RenderSortKey2D Key(int orderInLayer)
    {
        return new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, orderInLayer, 0, 0, 0, 0);
    }

    private static List<(float, float)> InstallRecorder(SpriteRendererComponent component)
    {
        var poses = new List<(float, float)>();
        component.AlphaWindowWriter = (min, max) => poses.Add((min, max));
        return poses;
    }

    /// <summary>
    /// The part of a draw path that touches the graphics device cannot run without one: it fails on the first device
    /// access, after the window was posed. The pose is what the tests observe.
    /// </summary>
    private static void CallDeviceFacing(Action drawPath)
    {
        try
        {
            drawPath();
        }
        catch (NullReferenceException)
        {
        }
        catch (TargetInvocationException exception) when (exception.InnerException is NullReferenceException)
        {
        }
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    private static Sprite CreateSprite(SpritePsxSemiTransparency mode = SpritePsxSemiTransparency.None)
    {
        var texture = new CasaEngine.Framework.Assets.Textures.Texture { Resource = CreateTexture() };
        var spriteData = new SpriteData
        {
            PositionInTexture = SourceRectangle,
            Origin = Point.Zero,
            PsxSemiTransparency = mode,
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
        var component = new SpriteRendererComponent(game);

        // DrawSprite(...) overloads that read the device scissor go through GraphicsDevice: a service whose device is
        // an uninitialized object answers ScissorRectangle with an empty rectangle.
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

    private static AnimatedSpriteComponent CreateAnimatedSprite(
        SpritePsxSemiTransparency mode, bool withDepthSortable, out SpriteRendererComponent renderer)
    {
        renderer = CreateComponent(out _);

        var world = new CasaEngine.Framework.Scene.World.World();
        var entityRoot = new TestSceneComponent();
        var entity = new Entity { RootComponent = entityRoot };
        SetProperty(entity, nameof(Entity.World), world);

        var component = new AnimatedSpriteComponent();
        entityRoot.AddChildComponent(component);
        SetField(component, "_spriteRenderer", renderer);
        if (withDepthSortable)
        {
            SetField(component, "_depthSortable2DComponent", new DepthSortable2DComponent
            {
                SortMode = DepthSortMode2D.TopDownYUp,
                LocalSortOffset = 0,
            });
        }

        var spriteId = Guid.Parse("c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0");
        var spriteById = (Dictionary<Guid, Sprite>)typeof(AnimatedSpriteComponent)
            .GetField("_spriteById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
        spriteById[spriteId] = CreateSprite(mode);

        var animationData = new Animation2dData();
        animationData.Parts.Add(new Animation2dPartData { Id = "body", DefaultSpriteId = spriteId });
        component.AddAnimation(new Animation2d(animationData));
        component.SetCurrentAnimation(0, true);
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

    private static void SetField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, arguments);
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
