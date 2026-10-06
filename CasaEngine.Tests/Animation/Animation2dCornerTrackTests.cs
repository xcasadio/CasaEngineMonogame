using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Covers the corner track of <c>.anim2d</c> (E19.g G2b-2, ADR-0069): the model and its JSON (written only when not empty, a
/// disabled key without corners), the sampler, the composition adapter, the duration, the bounds, the editor snapshot and the
/// draw of a part with active corners through <c>DrawPsxQuad</c> (batch level, no GPU). A file of today (no corner track)
/// is read and rewritten byte for byte.
/// </summary>
public class Animation2dCornerTrackTests
{
    private static readonly Guid SpriteId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string ActiveKeyJson = """
        {
          "time_seconds": 0.125,
          "enabled": true,
          "top_left": { "x": -12.0, "y": 12.0 },
          "top_right": { "x": 12.0, "y": 11.0 },
          "bottom_left": { "x": -10.0, "y": -12.0 },
          "bottom_right": { "x": 13.0, "y": -9.0 },
          "source_offset": { "x": -1, "y": 0 }
        }
        """;

    private const string DisabledKeyJson = """
        { "time_seconds": 0.375, "enabled": false }
        """;

    // ---- model, load, save ----

    [Fact]
    public void ActiveKey_IsLoadedWithEveryField()
    {
        var animation = LoadAnimation(CornerTrackJson(ActiveKeyJson, DisabledKeyJson));

        var track = Assert.Single(animation.Tracks, t => t.Property == Animation2dTrackProperty.Corners);
        Assert.Equal(2, track.CornerKeyframes.Count);
        var key = track.CornerKeyframes[0];
        Assert.Equal(0.125f, key.TimeSeconds);
        Assert.True(key.Enabled);
        Assert.Equal(new Vector2(-12f, 12f), key.TopLeft);
        Assert.Equal(new Vector2(12f, 11f), key.TopRight);
        Assert.Equal(new Vector2(-10f, -12f), key.BottomLeft);
        Assert.Equal(new Vector2(13f, -9f), key.BottomRight);
        Assert.Equal(new Point(-1, 0), key.SourceOffset);
    }

    [Fact]
    public void MissingFields_ReadAsZero_AndAMissingEnabledIsFalse()
    {
        var animation = LoadAnimation(CornerTrackJson("""{ "time_seconds": 0.2 }""", """{ "time_seconds": 0.4, "enabled": true }"""));

        var keys = animation.Tracks.Single(t => t.Property == Animation2dTrackProperty.Corners).CornerKeyframes;
        Assert.Equal(new Animation2dCornersKeyframeData(0.2f, false, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, Point.Zero), keys[0]);
        Assert.Equal(new Animation2dCornersKeyframeData(0.4f, true, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, Point.Zero), keys[1]);
    }

    [Fact]
    public void SourceOffset_AcceptsAnyInteger()
    {
        var key = LoadAnimation(CornerTrackJson("""{ "time_seconds": 0, "enabled": true, "source_offset": { "x": 3, "y": -7 } }"""))
            .Tracks.Single(t => t.Property == Animation2dTrackProperty.Corners).CornerKeyframes[0];

        Assert.Equal(new Point(3, -7), key.SourceOffset);
    }

    // ---- sampler and adapter ----

    [Fact]
    public void Sampler_PartIsARectangleBeforeTheFirstKey_AndOnADisabledKey()
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(
            LoadAnimation(CornerTrackJson("""{ "time_seconds": 0.1, "enabled": true, "top_left": { "x": 1, "y": 2 } }""", DisabledKeyJson))));

        Assert.False(sampler.RuntimeState.GetPart(0).HasCorners);

        sampler.Seek(0.1f);
        var part = sampler.RuntimeState.GetPart(0);
        Assert.True(part.HasCorners);
        Assert.Equal(new Vector2(1f, 2f), part.CornerTopLeft);

        sampler.Seek(0.375f);
        Assert.False(sampler.RuntimeState.GetPart(0).HasCorners);
    }

    [Fact]
    public void Sampler_TakesTheLastKeyAtTheTimeOrBefore_AllFieldsIncluded()
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(
            LoadAnimation(CornerTrackJson(ActiveKeyJson, DisabledKeyJson))));

        sampler.Seek(0.2f);

        var part = sampler.RuntimeState.GetPart(0);
        Assert.True(part.HasCorners);
        Assert.Equal(new Vector2(-12f, 12f), part.CornerTopLeft);
        Assert.Equal(new Vector2(12f, 11f), part.CornerTopRight);
        Assert.Equal(new Vector2(-10f, -12f), part.CornerBottomLeft);
        Assert.Equal(new Vector2(13f, -9f), part.CornerBottomRight);
        Assert.Equal(new Point(-1, 0), part.CornerSourceOffset);
    }

    [Fact]
    public void Sampler_ResetPutsTheCornersBack()
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(
            LoadAnimation(CornerTrackJson(ActiveKeyJson, DisabledKeyJson))));
        sampler.Seek(0.2f);
        Assert.True(sampler.RuntimeState.GetPart(0).HasCorners);

        sampler.Reset();

        var part = sampler.RuntimeState.GetPart(0);
        Assert.False(part.HasCorners);
        Assert.Equal(Vector2.Zero, part.CornerTopLeft);
        Assert.Equal(Point.Zero, part.CornerSourceOffset);
    }

    [Fact]
    public void Adapter_CloneKeepsTheCornerKeys()
    {
        var animation = LoadAnimation(CornerTrackJson(ActiveKeyJson, DisabledKeyJson));

        var composition = Animation2dCompositionAdapter.Create(animation);

        var cloned = composition.Tracks.Single(t => t.Property == Animation2dTrackProperty.Corners);
        Assert.Equal(2, cloned.CornerKeyframes.Count);
        Assert.Equal(animation.Tracks.Single(t => t.Property == Animation2dTrackProperty.Corners).CornerKeyframes, cloned.CornerKeyframes);
    }

    // ---- duration and bounds ----

    [Fact]
    public void Duration_IsUnchanged_WhenTheCornerKeysFallOnTheSpriteKeyTimes()
    {
        var animation = LoadAnimation(CornerTrackJson("""{ "time_seconds": 0, "enabled": true }""", """{ "time_seconds": 0.5, "enabled": false }"""));

        Assert.Equal(0.5f, animation.GetDurationSeconds());
    }

    [Fact]
    public void Duration_FollowsALaterCornerKey()
    {
        var animation = LoadAnimation(CornerTrackJson("""{ "time_seconds": 0.9, "enabled": true }"""));

        Assert.Equal(0.9f, animation.GetDurationSeconds());
    }

    [Fact]
    public void Bounds_OfAPartWithCorners_AreTheBoxOfItsCorners()
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(LoadAnimation(CornerTrackJson(ActiveKeyJson, DisabledKeyJson))));
        var sprites = new Dictionary<Guid, SpriteData> { [SpriteId] = SpriteWith(new Rectangle(0, 0, 4, 4), new Point(2, 2)) };

        sampler.Seek(0.2f);
        Assert.True(Animation2dBoundsCalculator.TryCalculateLocalBounds(sampler.RuntimeState, sprites, out var bounds));

        // Position (100, 50) + the four corners: x in [100 - 12, 100 + 13], y in [50 - 12, 50 + 12].
        Assert.Equal(88f, bounds.Min.X);
        Assert.Equal(113f, bounds.Max.X);
        Assert.Equal(38f, bounds.Min.Y);
        Assert.Equal(62f, bounds.Max.Y);
    }

    [Fact]
    public void Bounds_OfAPartWithoutCorners_AreTheRectangleOfToday()
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(LoadAnimation(CornerTrackJson(ActiveKeyJson, DisabledKeyJson))));
        var sprites = new Dictionary<Guid, SpriteData> { [SpriteId] = SpriteWith(new Rectangle(0, 0, 4, 4), new Point(2, 2)) };

        sampler.Seek(0.375f);
        Assert.True(Animation2dBoundsCalculator.TryCalculateLocalBounds(sampler.RuntimeState, sprites, out var bounds));

        Assert.Equal(98f, bounds.Min.X);
        Assert.Equal(102f, bounds.Max.X);
        Assert.Equal(48f, bounds.Min.Y);
        Assert.Equal(52f, bounds.Max.Y);
    }

    // ---- draw (batch level) ----

    [Fact]
    public void PartWithCorners_IsQueuedAsAPsxQuad_RectanglePartsStayOnTheSpritePath_SortKeyPath()
    {
        var fixture = CreateDrawFixture(withSortKey: true);

        fixture.Sampler.Seek(0.2f);
        fixture.Component.Draw(0f);

        var entries = GetSpriteDatas(fixture.Renderer);
        Assert.Equal(2, entries.Count);
        // Part order: DrawOrder 0 first (the quad part), then DrawOrder 1 (the rectangle part).
        var quad = entries[0]!;
        var rect = entries[1]!;
        Assert.True((bool)GetField(quad, "PsxQuad"));
        Assert.True((bool)GetField(quad, "NoCull"));
        Assert.False((bool)GetField(rect, "PsxQuad"));
        Assert.True((bool)GetField(quad, "HasSortKey"));
        var runtime = fixture.Sampler.RuntimeState;
        var expectedKey = AnimatedSpriteComponent.BuildPartSortKey(fixture.Component.BuildComposedAnimationBaseSortKey(), runtime.GetPart(0));
        Assert.Equal(expectedKey, (RenderSortKey2D)GetField(quad, "SortKey"));
        Assert.Equal(Color.Orange, (Color)GetField(quad, "Color"));
        Assert.Equal(3f, ((Matrix)GetField(quad, "WorldMatrix")).Translation.Z);
    }

    [Fact]
    public void PartWithCorners_IsQueuedAsAPsxQuad_ZOrderPath()
    {
        var fixture = CreateDrawFixture(withSortKey: false);

        fixture.Sampler.Seek(0.2f);
        fixture.Component.Draw(0f);

        var entries = GetSpriteDatas(fixture.Renderer);
        var quad = entries[0]!;
        Assert.True((bool)GetField(quad, "PsxQuad"));
        Assert.False((bool)GetField(quad, "HasSortKey"));
        Assert.Equal(3f - 0 * 0.0001f - 0 * 0.000001f, ((Matrix)GetField(quad, "WorldMatrix")).Translation.Z);
    }

    [Fact]
    public void QuadCorners_AreThePartPositionPlusTheCornersTimesTheEntityScale_WindowShiftedBySourceOffset()
    {
        var fixture = CreateDrawFixture(withSortKey: false);
        fixture.Sampler.Seek(0.2f);

        fixture.Component.Draw(0f);

        var vertices = FillVertices(fixture.Renderer, 2);
        // Entity at (10, 20), scale (2, 3), part position (100, 50): world part position (210, 170).
        var partPosition = new Vector2(10f + 100f * 2f, 20f + 50f * 3f);
        var tl = partPosition + new Vector2(-12f, 12f) * new Vector2(2f, 3f);
        var tr = partPosition + new Vector2(12f, 11f) * new Vector2(2f, 3f);
        var bl = partPosition + new Vector2(-10f, -12f) * new Vector2(2f, 3f);
        var br = partPosition + new Vector2(13f, -9f) * new Vector2(2f, 3f);
        var center = (tl + tr + bl + br) * 0.25f;
        Assert.Equal(new Vector3(tr - center, 0f), vertices[0].Position);
        Assert.Equal(new Vector3(br - center, 0f), vertices[1].Position);
        Assert.Equal(new Vector3(bl - center, 0f), vertices[2].Position);
        Assert.Equal(new Vector3(tl - center, 0f), vertices[3].Position);

        // The window of the sprite (8, 16, 10, 12) moved by the source offset (-1, 0), + half a texel + 1/4096.
        const float shift = 0.5f + 1f / 4096f;
        var left = (7 + shift) / 64f;
        var right = (17 + shift) / 64f;
        var top = (16 + shift) / 64f;
        var bottom = (28 + shift) / 64f;
        Assert.Equal(new Vector2(right, top), vertices[0].TextureCoordinate);
        Assert.Equal(new Vector2(left, bottom), vertices[2].TextureCoordinate);
    }

    [Fact]
    public void QuadPart_IgnoresTheRotationTheFlipsAndTheSpriteEffectOfTheComponent()
    {
        var plain = CreateDrawFixture(withSortKey: false);
        plain.Sampler.Seek(0.2f);
        plain.Component.Draw(0f);
        var expected = FillVertices(plain.Renderer, 2).Take(4).ToArray();

        var twisted = CreateDrawFixture(withSortKey: false, rotationAndFlips: true);
        twisted.Component.SpriteEffect = SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically;
        twisted.Sampler.Seek(0.2f);
        twisted.Component.Draw(0f);
        var actual = FillVertices(twisted.Renderer, 2).Take(4).ToArray();

        Assert.Equal(expected, actual);
    }

    // ---- byte for byte rewrite of a real file ----

    [Fact]
    public void RealFileOfToday_IsRewrittenByteForByte()
    {
        var fixturePath = Path.Combine(GetRepositoryRoot(), "CasaEngine.Tests", "Animation", "Fixtures", "bankalundra_0_anim1_down.anim2d");
        Assert.True(File.Exists(fixturePath), $"Fixture not found: {fixturePath}");
        var original = File.ReadAllBytes(fixturePath);

        var directory = Path.Combine(Path.GetTempPath(), "casa-anim2d-rewrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = directory;
            var animation = new Animation2dData();
            animation.Load(JObject.Parse(File.ReadAllText(fixturePath)));

            Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var root));
            EditorAssetWriterService.SaveDocument("rewritten.anim2d", root);

            var rewritten = File.ReadAllBytes(Path.Combine(directory, "rewritten.anim2d"));
            Assert.Equal(original.Length, rewritten.Length);
            Assert.True(original.AsSpan().SequenceEqual(rewritten), "The rewritten file differs from the fixture.");
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, true);
        }
    }

    // ---- helpers ----

    private static string CornerTrackJson(params string[] cornerKeys)
    {
        return $$"""
            {
              "id": "99999999-0000-0000-0000-000000000001",
              "name": "corner_test",
              "animation_type": "Once",
              "event_track_name": "Track 4",
              "parts": [
                { "id": "p0", "name": "p0", "default_sprite_id": "{{SpriteId}}", "default_position": { "x": 0.0, "y": 0.0 }, "default_rotation": 0.0, "default_draw_order": 0, "default_visible": true, "default_flip_x": false, "default_flip_y": false }
              ],
              "tracks": [
                { "name": "Track 1", "target_part_id": "p0", "property": "Sprite", "interpolation": "Step",
                  "sprite_keyframes": [ { "time_seconds": 0.0, "value": "{{SpriteId}}" }, { "time_seconds": 0.5, "value": "{{SpriteId}}" } ] },
                { "name": "Track 2", "target_part_id": "p0", "property": "Position", "interpolation": "Step",
                  "position_keyframes": [ { "time_seconds": 0.0, "value": { "x": 100.0, "y": 50.0 } } ] },
                { "name": "Track 3", "target_part_id": "p0", "property": "Corners", "interpolation": "Step",
                  "corner_keyframes": [ {{string.Join(",", cornerKeys)}} ] }
              ]
            }
            """;
    }

    private static Animation2dData LoadAnimation(string json)
    {
        var animation = new Animation2dData();
        animation.Load(JObject.Parse(json));
        return animation;
    }

    private static SpriteData SpriteWith(Rectangle window, Point origin)
    {
        return new SpriteData { PositionInTexture = window, Origin = origin };
    }

    private sealed record DrawFixture(
        AnimatedSpriteComponent Component, SpriteRendererComponent Renderer, Animation2dCompositionSampler Sampler);

    /// <summary>
    /// An entity at (10, 20, 3) with scale (2, 3), one composed animation with two parts: p0 (DrawOrder 0, corner track, position
    /// (100, 50)) and p1 (DrawOrder 1, no corner track). Sprites are built without a GraphicsDevice.
    /// </summary>
    private static DrawFixture CreateDrawFixture(bool withSortKey, bool rotationAndFlips = false)
    {
        var animationData = LoadAnimation(DrawAnimationJson(rotationAndFlips));
        var world = new CasaEngine.Framework.Scene.World.World();
        var entityRoot = new TestSceneComponent();
        var entity = new Entity { RootComponent = entityRoot };
        SetProperty(entity, nameof(Entity.World), world);

        var component = new AnimatedSpriteComponent();
        component.LocalPosition = new Vector3(10f, 20f, 3f);
        component.LocalScale = new Vector3(2f, 3f, 1f);
        component.Color = Color.Orange;
        entityRoot.AddChildComponent(component);
        if (withSortKey)
        {
            SetPrivateField(component, "_depthSortable2DComponent", new DepthSortable2DComponent
            {
                SortMode = DepthSortMode2D.TopDownYUp,
                LocalSortOffset = 0,
            });
        }

        component.AddAnimation(new CasaEngine.Framework.Assets.Animations.Animation2d(animationData));
        component.SetCurrentAnimation(0, true);

        var renderer = CreateRenderer();
        SetPrivateField(component, "_spriteRenderer", renderer);

        var spriteById = (Dictionary<Guid, Sprite>)typeof(AnimatedSpriteComponent)
            .GetField("_spriteById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
        spriteById[SpriteId] = CreateSprite(new Rectangle(8, 16, 10, 12), new Point(5, 6));

        var field = typeof(AnimatedSpriteComponent).GetField("_currentCompositionSampler", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return new DrawFixture(component, renderer, (Animation2dCompositionSampler)field.GetValue(component)!);
    }

    private static string DrawAnimationJson(bool rotationAndFlips)
    {
        var extraTracks = rotationAndFlips
            ? """
              ,{ "name": "Track 7", "target_part_id": "p0", "property": "Rotation", "interpolation": "Step", "rotation_keyframes": [ { "time_seconds": 0.0, "value": 1.0 } ] },
              { "name": "Track 8", "target_part_id": "p0", "property": "FlipX", "interpolation": "Step", "flip_keyframes": [ { "time_seconds": 0.0, "value": true } ] },
              { "name": "Track 9", "target_part_id": "p0", "property": "FlipY", "interpolation": "Step", "flip_keyframes": [ { "time_seconds": 0.0, "value": true } ] }
              """
            : string.Empty;
        return $$"""
            {
              "id": "99999999-0000-0000-0000-000000000003",
              "name": "draw_test",
              "animation_type": "Once",
              "event_track_name": "Track 10",
              "parts": [
                { "id": "p0", "name": "p0", "default_sprite_id": "{{SpriteId}}", "default_position": { "x": 0.0, "y": 0.0 }, "default_rotation": 0.0, "default_draw_order": 0, "default_visible": true, "default_flip_x": false, "default_flip_y": false },
                { "id": "p1", "name": "p1", "default_sprite_id": "{{SpriteId}}", "default_position": { "x": 0.0, "y": 0.0 }, "default_rotation": 0.0, "default_draw_order": 1, "default_visible": true, "default_flip_x": false, "default_flip_y": false }
              ],
              "tracks": [
                { "name": "Track 1", "target_part_id": "p0", "property": "Sprite", "interpolation": "Step",
                  "sprite_keyframes": [ { "time_seconds": 0.0, "value": "{{SpriteId}}" }, { "time_seconds": 0.5, "value": "{{SpriteId}}" } ] },
                { "name": "Track 2", "target_part_id": "p0", "property": "Position", "interpolation": "Step",
                  "position_keyframes": [ { "time_seconds": 0.0, "value": { "x": 100.0, "y": 50.0 } } ] },
                { "name": "Track 3", "target_part_id": "p0", "property": "Corners", "interpolation": "Step",
                  "corner_keyframes": [ {{ActiveKeyJson}}, {{DisabledKeyJson}} ] }
                {{extraTracks}}
              ]
            }
            """;
    }

    private static Sprite CreateSprite(Rectangle window, Point origin)
    {
        var texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        SetNonPublicField(texture, typeof(Texture2D), "width", 64);
        SetNonPublicField(texture, typeof(Texture2D), "height", 64);
        var sheet = new CasaEngine.Framework.Assets.Textures.Texture { Resource = texture };
        var spriteData = new SpriteData { PositionInTexture = window, Origin = origin };

        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        SetBackingField(sprite, nameof(Sprite.Texture), sheet);
        SetBackingField(sprite, nameof(Sprite.SpriteData), spriteData);
        return sprite;
    }

    private static SpriteRendererComponent CreateRenderer()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetNonPublicField(game, typeof(Game), "_components", new GameComponentCollection());
        var component = new SpriteRendererComponent(game);

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

    private static VertexPositionTexture[] FillVertices(SpriteRendererComponent renderer, int entryCount)
    {
        var count = renderer.FillVertices();
        Assert.Equal(entryCount * 4, count);
        var field = typeof(SpriteRendererComponent).GetField("_vertices", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (VertexPositionTexture[])field!.GetValue(renderer)!;
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent renderer)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(renderer)!;
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

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root (CasaEngine.MonoGame.sln) not found from " + AppContext.BaseDirectory);
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
