using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace DemosGame.Demos;

public class TileMapDemo : Demo
{
    public override string Title => "Tile map demo";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ tileMap ===============
        var tileMapData = TileMapLoader.LoadMapFromFile(@"Maps\map_1_1_tile_set.tileMap");

        var entity = new Entity();
        entity.Name = "TileMap";
        entity.Coordinates.LocalPosition = new Vector3(0, 700, 0.0f);
        var tileMapComponent = new TileMapComponent(entity);
        tileMapComponent.TileMapData = tileMapData;
        entity.ComponentManager.Components.Add(tileMapComponent);

        world.AddEntityImmediately(entity);

        //============ player ===============
        entity = new Entity();
        entity.Name = "Link";
        entity.Coordinates.LocalPosition = new Vector3(100, 550, 0.2f);
        var physicsComponent = new Physics2dComponent(entity);
        entity.ComponentManager.Components.Add(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        physicsComponent.PhysicsDefinition.Mass = 1.0f;
        physicsComponent.Shape = new ShapeCircle(25);
        physicsComponent.PhysicsDefinition.ApplyGravity = false;
        physicsComponent.PhysicsDefinition.AngularFactor = Vector3.Zero;
        var sprites = SpriteLoader.LoadFromFile("Content\\TileSets\\RPG.spritesheet", game.GameManager.AssetContentManager, SaveOption.Editor);
        var animations = Animation2dLoader.LoadFromFile("Content\\TileSets\\RPG.anims2d", game.GameManager.AssetContentManager);

        var animatedSprite = new AnimatedSpriteComponent(entity);
        entity.ComponentManager.Components.Add(animatedSprite);
        foreach (var animation in animations)
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        entity.ComponentManager.Components.Add(new PlayerComponent(entity));

        world.AddEntityImmediately(entity);
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new Camera3dIn2dAxisComponent(entity);
        camera.Target = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.ComponentManager.Components.Add(camera);
        game.GameManager.CurrentWorld.AddEntityImmediately(entity);

        return camera;
    }

    public override void Update(GameTime gameTime)
    {

    }

    protected override void Clean()
    {

    }
}