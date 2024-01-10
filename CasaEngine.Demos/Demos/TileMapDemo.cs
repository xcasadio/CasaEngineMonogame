using System.Collections.Generic;
using System.Linq;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

public class TileMapDemo : Demo
{
    public override string Title => "Tile map demo";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ tileMap ===============
        var tileMapData = TileMapLoader.LoadMapFromFile(@"Maps\map_1_1.tileMap");

        var entity = new Entity();
        entity.Name = "TileMap";
        entity.Coordinates.LocalPosition = new Vector3(0, 700, 0.0f);
        var tileMapComponent = new TileMapComponent();
        tileMapComponent.TileMapData = tileMapData;
        entity.ComponentManager.Components.Add(tileMapComponent);

        world.AddEntityImmediately(entity);

        //============ player ===============
        entity = new Entity();
        entity.Name = "Link";
        entity.Coordinates.LocalPosition = new Vector3(100, 550, 0.3f);
        var physicsComponent = new Physics2dComponent();
        entity.ComponentManager.Components.Add(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        physicsComponent.PhysicsDefinition.Mass = 1.0f;
        physicsComponent.Shape = new ShapeCircle(25);
        physicsComponent.PhysicsDefinition.ApplyGravity = false;
        physicsComponent.PhysicsDefinition.AngularFactor = Vector3.Zero;
        //ressources
        LoadSprites(game.GameManager.AssetContentManager, game.GraphicsDevice);
        var animations = LoadAnimations(game.GameManager.AssetContentManager, game.GraphicsDevice);

        var animatedSprite = new AnimatedSpriteComponent();
        entity.ComponentManager.Components.Add(animatedSprite);
        foreach (var animation in animations)
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        entity.ComponentManager.Components.Add(new PlayerComponent());

        world.AddEntityImmediately(entity);
    }

    private void LoadSprites(AssetContentManager assetContentManager, GraphicsDevice graphicsDevice)
    {
        var spriteAssetInfos = GameSettings.AssetInfoManager.AssetInfos
            .Where(x => x.FileName.EndsWith(Constants.FileNameExtensions.Sprite));

        foreach (var assetInfo in spriteAssetInfos)
        {
            var spriteData = assetContentManager.Load<SpriteData>(assetInfo);
        }
    }

    private List<Animation2dData> LoadAnimations(AssetContentManager assetContentManager, GraphicsDevice graphicsDevice)
    {
        var animationsAssetInfos = GameSettings.AssetInfoManager.AssetInfos
            .Where(x => x.FileName.EndsWith(Constants.FileNameExtensions.Animation2d));

        var animations = new List<Animation2dData>();

        foreach (var assetInfo in animationsAssetInfos)
        {
            var animation2dData = assetContentManager.Load<Animation2dData>(assetInfo);
            animations.Add(animation2dData);
        }

        return animations;
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new Camera3dIn2dAxisComponent();
        camera.Target = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.ComponentManager.Components.Add(camera);
        game.GameManager.CurrentWorld.AddEntityImmediately(entity);

        return camera;
    }
    public override void InitializeCamera(CameraComponent camera)
    {
        //((Camera3dIn2dAxisComponent)camera).SetCamera(Vector3.Backward * 15 + Vector3.Up * 12, Vector3.Zero, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {

    }

    public override void Clean()
    {

    }
}