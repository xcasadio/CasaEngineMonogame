using CasaEngine.EditorServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the lowering of the 2d authoring shapes by the simulation space policy and the pose that
/// moved from the shapes to their <see cref="Collision2d"/> attachment.
/// </summary>
public class SimulationSpaceLoweringTests
{
    [Fact]
    public void Lower_Rectangle_ProducesABoxExtrudedByThePolicy()
    {
        var policy = new SimulationSpacePolicy();

        var box = Assert.IsType<Box>(policy.Lower(new ShapeRectangle(28f, 36f)));

        Assert.Equal(new Vector3(28f, 36f, 1f), box.Size);
    }

    [Fact]
    public void Lower_Rectangle_UsesTheConfiguredExtrusionDepth()
    {
        var policy = new SimulationSpacePolicy { ExtrusionDepth = 4.5f };

        var box = Assert.IsType<Box>(policy.Lower(new ShapeRectangle(2f, 3f)));

        Assert.Equal(new Vector3(2f, 3f, 4.5f), box.Size);
    }

    [Fact]
    public void Lower_Circle_ProducesASphereOfTheSameRadius()
    {
        var policy = new Planar2dSimulationSpacePolicy();

        var sphere = Assert.IsType<Sphere>(policy.Lower(new ShapeCircle(7)));

        Assert.Equal(7f, sphere.Radius);
    }

    [Theory]
    [InlineData(Shape2dType.Line)]
    [InlineData(Shape2dType.Polygone)]
    [InlineData(Shape2dType.Compound)]
    public void Lower_UnsupportedShape_Throws(Shape2dType shapeType)
    {
        var policy = new Identity3dSimulationSpacePolicy();
        Shape2d shape = shapeType switch
        {
            Shape2dType.Line => new ShapeLine(),
            Shape2dType.Polygone => new ShapePolygone(),
            _ => new Shape2dCompound()
        };

        Assert.Throws<NotSupportedException>(() => policy.Lower(shape));
    }

    [Fact]
    public void DefaultSpacePolicy_IsIdentity3d()
    {
        Assert.IsType<Identity3dSimulationSpacePolicy>(GameSettings.PhysicsEngineSettings.SpacePolicy);
    }

    [Fact]
    public void Collision2d_RoundTripsProfileTagAndPose()
    {
        var collision2d = new Collision2d
        {
            ProfileName = CollisionProfileNames.AttackVolume,
            Tag = "sword",
            LocalPosition = new Vector2(9f, 6f),
            Rotation = 1.25f,
            Shape = new ShapeRectangle(28f, 36f)
        };

        var node = new JObject();
        EditorEntityJsonSerializer.SaveCollision2d(collision2d, node);

        //The pose keys stay flat on the collision node, exactly where they used to be written.
        Assert.Equal("AttackVolume", (string?)node["collision_profile"]);
        Assert.Equal("sword", (string?)node["tag"]);
        Assert.Equal("Rectangle", (string?)node["shape_type"]);
        Assert.Equal(9f, node["location"]!["x"]!.Value<float>());
        Assert.Equal(6f, node["location"]!["y"]!.Value<float>());
        Assert.Equal(1.25f, node["orientation"]!.Value<float>());
        Assert.Null(node["collision_type"]);

        var loaded = new Collision2d();
        loaded.Load(node);

        Assert.Equal(CollisionProfileNames.AttackVolume, loaded.ProfileName);
        Assert.Equal("sword", loaded.Tag);
        Assert.Equal(new Vector2(9f, 6f), loaded.LocalPosition);
        Assert.Equal(1.25f, loaded.Rotation);
        var rectangle = Assert.IsType<ShapeRectangle>(loaded.Shape);
        Assert.Equal(28f, rectangle.Width);
        Assert.Equal(36f, rectangle.Height);
    }

    [Fact]
    public void Collision2d_WithoutProfileOrTag_LoadsEmptyDefaults()
    {
        var node = new JObject
        {
            ["shape_type"] = "Rectangle",
            ["location"] = new JObject { ["x"] = 0f, ["y"] = 0f },
            ["orientation"] = 0f,
            ["w"] = 4f,
            ["h"] = 5f
        };

        var collision2d = new Collision2d();
        collision2d.Load(node);

        Assert.Equal(string.Empty, collision2d.ProfileName);
        Assert.Equal(string.Empty, collision2d.Tag);
        Assert.Equal(CollisionProfileIds.Trigger, SpriteCollisionHelper.ResolveProfileId(collision2d));
    }

    [Fact]
    public void MigratedSpriteAsset_LoadsItsCollisionProfile()
    {
        string path = Path.Combine(
            FindRepositoryRoot(), "CasaEngine.Demos", "Content", "TileSets", "player_0.sprite");
        Assert.True(File.Exists(path), $"missing {path}");

        var spriteData = new SpriteData();
        spriteData.Load(JObject.Parse(File.ReadAllText(path)));

        var collision2d = Assert.Single(spriteData.CollisionShapes);
        Assert.Equal(CollisionProfileNames.AttackVolume, collision2d.ProfileName);
        Assert.Equal(new Vector2(9f, 6f), collision2d.LocalPosition);
        var rectangle = Assert.IsType<ShapeRectangle>(collision2d.Shape);
        Assert.Equal(28f, rectangle.Width);
        Assert.Equal(36f, rectangle.Height);
    }

    [Fact]
    public void SpriteVolumeProfiles_AreSensorsWithTheirDebugColors()
    {
        var profiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;

        var attack = profiles.GetResolved(CollisionProfileNames.AttackVolume);
        Assert.Equal(1u << CollisionChannels.AttackVolume, attack.GroupBit);
        Assert.Equal(ChannelMask.None, attack.BlockMask);
        Assert.Equal(ChannelMask.All, attack.OverlapMask);
        Assert.Equal(ChannelMask.All, attack.BroadphaseMask);
        Assert.True(attack.IsSensor);

        var damageable = profiles.GetResolved(CollisionProfileNames.DamageableVolume);
        Assert.Equal(1u << CollisionChannels.DamageableVolume, damageable.GroupBit);
        Assert.Equal(ChannelMask.None, damageable.BlockMask);
        Assert.Equal(ChannelMask.All, damageable.OverlapMask);
        Assert.True(damageable.IsSensor);

        Assert.Equal(Color.Red, profiles.GetProfile(CollisionProfileIds.AttackVolume).DebugColor);
        Assert.Equal(Color.Green, profiles.GetProfile(CollisionProfileIds.DamageableVolume).DebugColor);
    }

    [Fact]
    public void DebugColor_FollowsTheResolvedProfile()
    {
        var attack = new Collision2d { ProfileName = CollisionProfileNames.AttackVolume, Shape = new ShapeRectangle(1f, 1f) };
        var damageable = new Collision2d { ProfileName = CollisionProfileNames.DamageableVolume, Shape = new ShapeRectangle(1f, 1f) };
        var unnamed = new Collision2d { Shape = new ShapeRectangle(1f, 1f) };
        var unknown = new Collision2d { ProfileName = "NotAProfile", Shape = new ShapeRectangle(1f, 1f) };

        Assert.Equal(Color.Red, SpriteCollisionHelper.GetDebugColor(attack));
        Assert.Equal(Color.Green, SpriteCollisionHelper.GetDebugColor(damageable));
        //Empty and unknown names both fall back to the reserved Trigger profile.
        Assert.Equal(SpriteCollisionHelper.GetDebugColor(unnamed), SpriteCollisionHelper.GetDebugColor(unknown));
    }

    [Fact]
    public void SpriteBodyPlacement_MatchesTheHistoricalShapePoseMath()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new TestCollideableComponent();
        var collision2d = new Collision2d
        {
            LocalPosition = new Vector2(9f, 6f),
            Shape = new ShapeRectangle(28f, 36f)
        };

        using var body = SpriteCollisionHelper.CreateCollisionBody(
            collision2d, Vector3.One, Matrix.Identity, physicsWorldContext, component);

        var origin = new Point(23, 35);
        var position = new Vector3(4f, -2f, 3f);
        var scale = new Vector3(2f, 3f, 1f);
        SpriteCollisionHelper.UpdateBodyTransformation(position, Quaternion.Identity, scale, body, collision2d, origin, Rectangle.Empty);

        //Historical formula, with the pose read from the collision volume instead of the shape.
        float expectedX = position.X + (9f - origin.X + 28f / 2f) * scale.X;
        float expectedY = position.Y - (6f - origin.Y + 36f / 2f) * scale.Y;
        var translation = body.WorldTransform.Translation;
        Assert.Equal(expectedX, translation.X, 3);
        Assert.Equal(expectedY, translation.Y, 3);
        Assert.Equal(position.Z, translation.Z, 3);
    }

    [Fact]
    public void SpriteBody_WithoutProfileName_UsesTheTriggerProfile()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new TestCollideableComponent();
        var collision2d = new Collision2d { Shape = new ShapeRectangle(1f, 1f) };

        using var body = SpriteCollisionHelper.CreateCollisionBody(
            collision2d, Vector3.One, Matrix.Identity, physicsWorldContext, component);

        Assert.Equal(
            GameSettings.PhysicsEngineSettings.CollisionProfiles.GetResolved(CollisionProfileIds.Trigger).GroupBit,
            body.CollisionProfile.GroupBit);
    }

    /// <summary>
    /// A circle volume used to be created as a sphere and then crash on the first transform update,
    /// which only handled rectangles. This is the path <see cref="StaticSpriteComponent"/> runs every frame.
    /// </summary>
    [Fact]
    public void SpriteCircleVolume_IsCreatedAndPlacedByItsRadius()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new TestCollideableComponent();
        var collision2d = new Collision2d
        {
            LocalPosition = new Vector2(9f, 6f),
            Shape = new ShapeCircle { Radius = 12f }
        };

        using var body = SpriteCollisionHelper.CreateCollisionBody(
            collision2d, Vector3.One, Matrix.Identity, physicsWorldContext, component);
        Assert.NotNull(body);

        var origin = new Point(23, 35);
        var position = new Vector3(4f, -2f, 3f);
        var scale = new Vector3(2f, 3f, 1f);
        SpriteCollisionHelper.UpdateBodyTransformation(position, Quaternion.Identity, scale, body, collision2d, origin, Rectangle.Empty);

        //Same origin math as a rectangle, with the radius standing in for the half size.
        var translation = body.WorldTransform.Translation;
        Assert.Equal(position.X + (9f - origin.X + 12f) * scale.X, translation.X, 3);
        Assert.Equal(position.Y - (6f - origin.Y + 12f) * scale.Y, translation.Y, 3);
        Assert.Equal(position.Z, translation.Z, 3);
    }

    [Theory]
    [InlineData(0f, 0f, 8f, -8f)]
    [InlineData(2f, 3f, 10f, -11f)]
    [InlineData(-4f, 5f, 4f, -13f)]
    public void TileCollisionPlacement_AddsTheVolumePoseToTheTileOrigin(
        float localX, float localY, float expectedX, float expectedY)
    {
        var collision2d = new Collision2d
        {
            LocalPosition = new Vector2(localX, localY),
            Shape = new ShapeRectangle(16f, 16f)
        };
        var worldMatrix = Matrix.Identity;

        var position = TileMapComponent.ComputeTileCollisionWorldPosition(
            ref worldMatrix, collision2d, 0, 0, new CasaEngine.Core.Math.Size(16, 16));

        Assert.Equal(expectedX, position.X, 4);
        Assert.Equal(expectedY, position.Y, 4);
    }

    [Fact]
    public void TileCollisionPlacement_KeepsTheTileOriginOffset()
    {
        var collision2d = new Collision2d
        {
            LocalPosition = new Vector2(2f, 3f),
            Shape = new ShapeRectangle(10f, 11f)
        };
        var worldMatrix = Matrix.Identity;

        var position = TileMapComponent.ComputeTileCollisionWorldPosition(
            ref worldMatrix, collision2d, 3, 2, new CasaEngine.Core.Math.Size(16, 16));

        Assert.Equal(3 * 16 + 2f + 10f / 2f, position.X, 4);
        Assert.Equal(-(2 * 16 + 3f) - 11f / 2f, position.Y, 4);
    }

    private sealed class TestCollideableComponent : ICollideableComponent
    {
        public Entity Owner { get; } = new();

        public PhysicsType PhysicsType => PhysicsType.Kinetic;

        public HashSet<Collision> Collisions { get; } = [];
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("repo root not found");
    }
}
