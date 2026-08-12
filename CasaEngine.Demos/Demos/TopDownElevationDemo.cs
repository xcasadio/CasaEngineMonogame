using CasaEngine.Core.Logging;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Shows the simulation space policy of a world: the logical space of this one is
/// (X east, Y ground depth, Z elevation) and its render space folds Y and Z into a single screen axis
/// (render = (X, -(Y - Z), 0)).
/// Two ghost boxes are placed at the same screen row but sixteen units apart in elevation: they overlap
/// on screen and never collide. A third box, co-located with the lower one in the logical space, does
/// collide with it. A pure sprite, projected by a <see cref="RenderProjectionComponent"/>, renders at the
/// screen position derived from the logical position of its entity.
/// </summary>
public class TopDownElevationDemo : Demo
{
    // Both boxes derive the same screen row: -(20 - 0) = -(36 - 16) = -20.
    private static readonly Vector3 GroundLevelPosition = new(10f, 20f, 0f);
    private static readonly Vector3 ElevatedPosition = new(10f, 36f, 16f);
    private static readonly Vector3 SpritePosition = new(30f, 24f, 4f);
    private const float BoxSize = 4f;

    private World _world;
    private CollisionComponent _groundLevelBox;
    private CollisionComponent _elevatedBox;
    private CollisionComponent _coLocatedBox;
    private RenderProjectionComponent _spriteProjection;
    private string _lastReport;

    public override string Title => "Top down elevation demo";

    public override string Description =>
        "A world whose simulation space is (x, ground depth, elevation): two boxes overlapping on screen at different elevations never collide, co-located ones do.";

    public override void Initialize(CasaEngineGame game)
    {
        _world = game.GameManager.CurrentWorld;

        //The policy is read when the physics context of the world is created, which happens after this call.
        _world.SpacePolicyName = SimulationSpacePolicyNames.TopDownElevation;
        _lastReport = null;

        _groundLevelBox = AddGhostBox("ground_level_box", GroundLevelPosition);
        _elevatedBox = AddGhostBox("elevated_box", ElevatedPosition);
        _coLocatedBox = AddGhostBox("co_located_box", GroundLevelPosition);
        AddProjectedSprite(game);
    }

    /// <summary>
    /// A trigger box whose collision component is the root of its entity: its pose is the logical pose
    /// of the entity, the one the simulation reads.
    /// </summary>
    private CollisionComponent AddGhostBox(string name, Vector3 logicalPosition)
    {
        var entity = new Entity { Name = name };
        var collisionComponent = new CollisionComponent();
        collisionComponent.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        collisionComponent.PhysicsDefinition.ProfileName = CollisionProfileNames.Trigger;
        collisionComponent.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(BoxSize) }) { Tag = name });
        entity.RootComponent = collisionComponent;
        collisionComponent.LocalPosition = logicalPosition;
        _world.AddEntity(entity);

        return collisionComponent;
    }

    /// <summary>
    /// A pure visual: a sprite with no authored collision volume, attached under the projection component
    /// so that it renders where the policy says the logical position of the entity shows up on screen.
    /// </summary>
    private void AddProjectedSprite(CasaEngineGame game)
    {
        var assetInfo = AssetCatalog.GetByFileName(@"TileSets\player_100.sprite");
        if (assetInfo == null)
        {
            Logs.WriteWarning("[TopDownElevationDemo] Sprite asset 'TileSets\\player_100.sprite' was not found.");
            return;
        }

        game.AssetContentManager.Load<SpriteData>(assetInfo.Id);

        var entity = new Entity { Name = "projected_sprite" };
        var logicalAnchor = new CollisionComponent();
        logicalAnchor.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        logicalAnchor.PhysicsDefinition.ProfileName = CollisionProfileNames.Trigger;
        logicalAnchor.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(BoxSize) }) { Tag = "projected_sprite" });
        entity.RootComponent = logicalAnchor;
        logicalAnchor.LocalPosition = SpritePosition;

        _spriteProjection = new RenderProjectionComponent();
        logicalAnchor.AddChildComponent(_spriteProjection);

        //The sprite is authored in pixels: scale it down to the units this demo simulates in.
        var spriteComponent = new StaticSpriteComponent { SpriteAssetId = assetInfo.Id };
        spriteComponent.LocalScale = new Vector3(0.15f);
        _spriteProjection.AddChildComponent(spriteComponent);

        _world.AddEntity(entity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        //Frame both spaces at once: the debug volumes, which live in the logical space (y from 18 to 38),
        //and the render plane, where everything projects on the screen row y = -20.
        var target = new Vector3(18f, 6f, 0f);
        ((ArcBallCameraComponent)camera).SetCamera(target + Vector3.Backward * 95f, target, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_groundLevelBox == null)
        {
            return;
        }

        string report =
            $"elevation separated pair: {DescribeCollision(_groundLevelBox, _elevatedBox)}, " +
            $"co-located pair: {DescribeCollision(_groundLevelBox, _coLocatedBox)}, " +
            $"projected sprite at {(_spriteProjection != null ? _spriteProjection.Position.ToString() : "none")}";

        if (report != _lastReport)
        {
            _lastReport = report;
            Logs.WriteInfo($"[TopDownElevationDemo] {report}");
        }
    }

    private static string DescribeCollision(CollisionComponent a, CollisionComponent b)
    {
        foreach (var collision in a.Collisions)
        {
            if (ReferenceEquals(collision.ColliderA, b) || ReferenceEquals(collision.ColliderB, b))
            {
                return "colliding";
            }
        }

        return "not colliding";
    }

    public override void Clean()
    {
        //The demos share one world: give it back the default policy of the project.
        if (_world != null)
        {
            _world.SpacePolicyName = string.Empty;
            _world = null;
        }

        _groundLevelBox = null;
        _elevatedBox = null;
        _coLocatedBox = null;
        _spriteProjection = null;
    }
}
