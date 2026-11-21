using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// What a static sprite queues on the renderer: the four corners of its quad, as the world matrix of the queue entry
/// places them (the renderer keeps the unit quad and gives that matrix to the shader). No GPU: the queue is inspected,
/// with the harness of <see cref="SpriteComponentDrawOrderTests"/>. The asset catalogue is global state, so this class runs
/// in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class StaticSpriteWorldTransformTests : IDisposable
{
    private static readonly Guid SpriteId = Guid.Parse("c3000000-0000-4000-8000-000000000001");
    private static readonly Guid SheetId = Guid.Parse("c3000000-0000-4000-8000-000000000002");
    private static readonly Guid ImageId = Guid.Parse("c3000000-0000-4000-8000-000000000003");
    private const string SpriteName = "transform-sprite";

    //  The corners of the unit quad of the renderer, in its local space.
    private static readonly Vector3[] LocalCorners =
    [
        new(-0.5f, 0.5f, 0f), new(0.5f, 0.5f, 0f), new(0.5f, -0.5f, 0f), new(-0.5f, -0.5f, 0f),
    ];

    //  The sprite data the loader hands out: set by each test before the sprite is resolved.
    private static Rectangle _source = new(0, 0, 16, 16);
    private static Point _origin = Point.Zero;

    public StaticSpriteWorldTransformTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(SpriteId) { Name = SpriteName, FileName = "transform-sprite.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(SheetId) { Name = "transform-sheet", FileName = "transform-sheet.texture" });
        AssetCatalog.AddInternal(new AssetInfo(ImageId) { Name = "transform-image", FileName = "transform-image.png" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    // ---- The unrotated sprite: what it queued before it was drawn with its world transform ----

    public enum Placement
    {
        Root,
        ChildComponent,
        ChildEntity,
    }

    public enum Mode
    {
        Opaque,
        Blended,
        OpaqueWithDrawOrder,
        BlendedWithDrawOrder,
        OpaqueWithSortKey,
        BlendedWithSortKey,
    }

    private static readonly Vector3[] Positions =
    [
        Vector3.Zero, new(12f, -7f, 3f), new(100.25f, 40.75f, -12.5f), new(-1234.5f, 987.125f, 250f), new(0.3f, 0.7f, 0.1f),
    ];

    private static readonly Vector3[] Scales =
    [
        Vector3.One, new(2f, 3f, 1f), new(0.15f, 0.15f, 0.15f), new(-1f, 1f, 1f), new(1.5f, -0.5f, 4f),
    ];

    private static readonly (Rectangle Source, Point Origin)[] Sprites =
    [
        (new Rectangle(0, 0, 16, 16), Point.Zero),
        (new Rectangle(8, 4, 48, 32), new Point(24, 16)),
        (new Rectangle(0, 0, 511, 257), new Point(7, 250)),
    ];

    private static readonly Vector3[] ParentPositions = [new(50f, -20f, 5f), new(-300.5f, 12.25f, 0f)];

    [Theory]
    [InlineData(Placement.Root, Mode.Opaque)]
    [InlineData(Placement.Root, Mode.Blended)]
    [InlineData(Placement.Root, Mode.OpaqueWithDrawOrder)]
    [InlineData(Placement.Root, Mode.BlendedWithDrawOrder)]
    [InlineData(Placement.Root, Mode.OpaqueWithSortKey)]
    [InlineData(Placement.Root, Mode.BlendedWithSortKey)]
    [InlineData(Placement.ChildComponent, Mode.Opaque)]
    [InlineData(Placement.ChildComponent, Mode.Blended)]
    [InlineData(Placement.ChildComponent, Mode.OpaqueWithDrawOrder)]
    [InlineData(Placement.ChildComponent, Mode.BlendedWithDrawOrder)]
    [InlineData(Placement.ChildEntity, Mode.Opaque)]
    [InlineData(Placement.ChildEntity, Mode.Blended)]
    [InlineData(Placement.ChildEntity, Mode.OpaqueWithDrawOrder)]
    [InlineData(Placement.ChildEntity, Mode.BlendedWithDrawOrder)]
    public void AnUnrotatedSprite_QueuesTheCornersOfItsPositionAndScale(Placement placement, Mode mode)
    {
        int cases = 0;
        foreach (var (source, origin) in Sprites)
        {
            foreach (Vector3 position in Positions)
            {
                foreach (Vector3 scale in Scales)
                {
                    Vector3[] parents = placement == Placement.Root ? [Vector3.Zero] : ParentPositions;
                    foreach (Vector3 parentPosition in parents)
                    {
                        _source = source;
                        _origin = origin;
                        var harness = new Harness();
                        StaticSpriteComponent sprite = harness.Place(placement, mode, position, scale, parentPosition);

                        sprite.Submit(Rectangle.Empty);

                        Matrix world = harness.SingleWorldMatrix();
                        //  What the sprite passed to the renderer before: the sum of the positions and its own scale.
                        Vector3 worldPosition = position + parentPosition;
                        string context = $"({placement}, {mode}, source {source}, origin {origin}, position {position}, scale {scale}, parent {parentPosition})";
                        AssertCorners(ExpectedCorners(source, origin, worldPosition, scale), world, context);
                        cases++;
                    }
                }
            }
        }

        Assert.True(cases >= 75, $"{cases} cases");
    }

    //  The formula of the renderer for a position, a scale and no rotation, in the order of its code:
    //  centre ((x - ox * sx) + (w / 2) * sx, (y + oy * sy) - (h / 2) * sy, z), size (w * sx, h * sy).
    private static Vector3[] ExpectedCorners(Rectangle source, Point origin, Vector3 position, Vector3 scale)
    {
        float centreX = position.X - origin.X * scale.X + (source.Width / 2f) * scale.X;
        float centreY = position.Y + origin.Y * scale.Y - (source.Height / 2f) * scale.Y;
        float width = scale.X * source.Width;
        float height = scale.Y * source.Height;

        var corners = new Vector3[LocalCorners.Length];
        for (int index = 0; index < LocalCorners.Length; index++)
        {
            corners[index] = new Vector3(centreX + LocalCorners[index].X * width, centreY + LocalCorners[index].Y * height, position.Z);
        }

        return corners;
    }

    //  The rule of the plan: x and y within 1e-4 * max(1, |expected|), z exactly.
    private static void AssertCorners(Vector3[] expected, Matrix world, string context)
    {
        for (int index = 0; index < LocalCorners.Length; index++)
        {
            Vector3 actual = Vector3.Transform(LocalCorners[index], world);
            AssertClose(expected[index].X, actual.X, $"corner {index} x {context}");
            AssertClose(expected[index].Y, actual.Y, $"corner {index} y {context}");
            Assert.True(expected[index].Z == actual.Z, $"corner {index} z: expected {expected[index].Z:R}, actual {actual.Z:R} {context}");
        }
    }

    private static void AssertClose(float expected, float actual, string context)
    {
        float tolerance = 1e-4f * MathF.Max(1f, MathF.Abs(expected));
        Assert.True(MathF.Abs(expected - actual) <= tolerance, $"{context}: expected {expected:R}, actual {actual:R}");
    }

    // ---- The renderer: a sprite submitted with a world transform and its settings ----

    private static readonly Matrix SomeWorld =
        Matrix.CreateScale(2f, 3f, 1f)
        * Matrix.CreateFromYawPitchRoll(0.6f, -0.3f, 0.25f)
        * Matrix.CreateTranslation(40f, -15f, 7f);

    //  What the renderer builds before it applies the world transform: the quad sized by the source rectangle and placed
    //  by the origin of the sprite.
    private static Matrix LocalQuad(Rectangle source, Point origin)
    {
        return Matrix.CreateScale(source.Width, source.Height, 1f)
            * Matrix.CreateTranslation(-origin.X + source.Width / 2f, origin.Y - source.Height / 2f, 0f);
    }

    [Fact]
    public void TheRenderer_PlacesTheQuadByTheWorldTransform_AndKeepsTheSettings()
    {
        _source = new Rectangle(8, 4, 48, 32);
        _origin = new Point(24, 16);
        var harness = new Harness();
        Sprite sprite = harness.SpriteOf(harness.Place(Placement.Root, Mode.Opaque, Vector3.Zero, Vector3.One, Vector3.Zero));
        var scissor = new Rectangle(1, 2, 30, 40);

        harness.Renderer.DrawSprite(sprite, in SomeWorld, Color.Coral, SpriteBlendMode.Additive, drawOrder: 5, scissor, drawDebug: false);

        object entry = harness.SingleEntry();
        AssertMatrix(LocalQuad(_source, _origin) * SomeWorld, Harness.Read<Matrix>(entry, "WorldMatrix"));
        Assert.Equal(Color.Coral, Harness.Read<Color>(entry, "Color"));
        Assert.Equal(SpriteBlendMode.Additive, Harness.Read<SpriteBlendMode>(entry, "BlendMode"));
        Assert.Equal(5, Harness.Read<int>(entry, "DrawOrder"));
        Assert.Equal(scissor, Harness.Read<Rectangle>(entry, "ScissorRectangle"));
        Assert.False(Harness.Read<bool>(entry, "HasSortKey"));
        Assert.False(Harness.Read<bool>(entry, "IgnoresDepth"));
        Assert.False(Harness.Read<bool>(entry, "NoCull"));
    }

    [Fact]
    public void TheRenderer_WithASortKey_KeepsTheKey()
    {
        _source = new Rectangle(0, 0, 16, 16);
        _origin = Point.Zero;
        var harness = new Harness();
        Sprite sprite = harness.SpriteOf(harness.Place(Placement.Root, Mode.Opaque, Vector3.Zero, Vector3.One, Vector3.Zero));
        var key = new RenderSortKey2D(3, 2, 1, 0, 42, 0, 7);

        harness.Renderer.DrawSprite(sprite, in SomeWorld, Color.White, in key, SpriteBlendMode.AlphaBlend, Rectangle.Empty, drawDebug: false);

        object entry = harness.SingleEntry();
        AssertMatrix(LocalQuad(_source, _origin) * SomeWorld, Harness.Read<Matrix>(entry, "WorldMatrix"));
        Assert.True(Harness.Read<bool>(entry, "HasSortKey"));
        Assert.Equal(key, Harness.Read<RenderSortKey2D>(entry, "SortKey"));
        Assert.Equal(SpriteBlendMode.AlphaBlend, Harness.Read<SpriteBlendMode>(entry, "BlendMode"));
    }

    private static void AssertMatrix(Matrix expected, Matrix actual)
    {
        float[] expectedTerms = Matrix.ToFloatArray(expected);
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expectedTerms.Length; index++)
        {
            AssertClose(expectedTerms[index], actualTerms[index], $"term {index}");
        }
    }

    // ---- Harness ----

    private static JObject SpriteNode(Mode mode)
    {
        bool blended = mode is Mode.Blended or Mode.BlendedWithDrawOrder or Mode.BlendedWithSortKey;
        bool withDrawOrder = mode is Mode.OpaqueWithDrawOrder or Mode.BlendedWithDrawOrder;
        var sprite = new StaticSpriteComponent { DrawOrder = withDrawOrder ? 3 : 0 };
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(new Entity { Name = "Unit", RootComponent = sprite }, node);
        var root = (JObject)node["root_component"]!;
        root["spriteDataName"] = SpriteName;
        if (blended)
        {
            root["blend_mode"] = "AlphaBlend";
        }

        return node;
    }

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new SpriteData { SpriteSheetAssetId = SheetId, PositionInTexture = _source, Origin = _origin };

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class SheetLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new Texture(ImageId, null);

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class ImageLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class Harness
    {
        private readonly World _world;
        private readonly SpriteRendererComponent _renderer;

        public Harness()
        {
            var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
            var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(componentsField);
            componentsField!.SetValue(game, new GameComponentCollection());

            var manager = new AssetContentManager();
            manager.RegisterAssetLoader(typeof(SpriteData), new SpriteDataLoader());
            manager.RegisterAssetLoader(typeof(Texture), new SheetLoader());
            manager.RegisterAssetLoader(typeof(Texture2D), new ImageLoader());
            SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), manager);

            _renderer = new SpriteRendererComponent(game);
            _world = new World();
            SetBackingField(_world, nameof(World.Game), game);
        }

        public World World => _world;

        public SpriteRendererComponent Renderer => _renderer;

        public Sprite SpriteOf(StaticSpriteComponent component)
        {
            var field = typeof(StaticSpriteComponent).GetField("_sprite", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (Sprite)field!.GetValue(component)!;
        }

        public object SingleEntry()
        {
            Assert.Single(Queue);
            return Queue[0]!;
        }

        public static T Read<T>(object entry, string name)
        {
            var field = entry.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (T)field!.GetValue(entry)!;
        }

        public System.Collections.IList Queue
        {
            get
            {
                var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field);
                return (System.Collections.IList)field!.GetValue(_renderer)!;
            }
        }

        public Matrix SingleWorldMatrix()
        {
            Assert.Single(Queue);
            object entry = Queue[0]!;
            var field = entry.GetType().GetField("WorldMatrix", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (Matrix)field!.GetValue(entry)!;
        }

        /// <summary>
        /// Builds the sprite at its place: root of its entity, child component of an inert root, or root of an entity
        /// that is the child of another entity. The parents carry a position only.
        /// </summary>
        public StaticSpriteComponent Place(Placement placement, Mode mode, Vector3 position, Vector3 scale, Vector3 parentPosition)
        {
            var spriteEntity = new Entity();
            spriteEntity.Load(SpriteNode(mode));
            var sprite = (StaticSpriteComponent)spriteEntity.RootComponent!;
            sprite.LocalPosition = position;
            sprite.LocalScale = scale;
            if (mode is Mode.OpaqueWithSortKey or Mode.BlendedWithSortKey)
            {
                spriteEntity.AddComponent(new DepthSortable2DComponent());
            }

            switch (placement)
            {
                case Placement.Root:
                    Initialise(spriteEntity);
                    return sprite;

                case Placement.ChildComponent:
                {
                    spriteEntity.RootComponent = null;
                    var root = new TransformComponent { LocalPosition = parentPosition };
                    var entity = new Entity { Name = "Holder", RootComponent = root };
                    root.AddChildComponent(sprite);
                    Initialise(entity);
                    return sprite;
                }

                default:
                {
                    var parent = new Entity { Name = "Parent", RootComponent = new TransformComponent { LocalPosition = parentPosition } };
                    parent.AddChild(spriteEntity);
                    Initialise(parent);
                    return sprite;
                }
            }
        }

        public void Initialise(Entity entity)
        {
            entity.Initialize();
            entity.InitializeWithWorld(_world);
        }

        private static void SetBackingField(object instance, string propertyName, object value)
        {
            var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(instance, value);
        }
    }
}
