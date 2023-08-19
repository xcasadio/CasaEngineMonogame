using System;
using System.IO;
using System.Linq;
using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RPGDemo.Controllers;
using RPGDemo.Scripts;

namespace RPGDemo;

public class RpgGame : CasaEngineGame
{
    protected override void Initialize()
    {
        GameSettings.ProjectSettings.ProjectPath = Path.Combine(Environment.CurrentDirectory, "Content");
        //new GridComponent(this);
        //new AxisComponent(this);
        base.Initialize();

        //IsMouseVisible = true;

        //GameManager.SpriteRendererComponent.IsDrawCollisionsEnabled = true;
        //GameManager.SpriteRendererComponent.IsDrawSpriteOriginEnabled = true;

        //GameManager.Renderer2dComponent.IsDrawCollisionsEnabled = true;
        //GameManager.Renderer2dComponent.IsDrawSpriteOriginEnabled = true;

        PhysicsDebugViewRendererComponent.DisplayPhysics = true;
    }

    protected override void LoadContent()
    {
        GameManager.DefaultSpriteFont = Content.Load<SpriteFont>("GizmoFont");

        var world = new World();
        GameManager.CurrentWorld = world;

        //============ Camera ===============
        var entity = new Entity();
        //var camera = new ArcBallCameraComponent(entity);
        //camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        var camera = new Camera3dIn2dAxisComponent(entity);
        camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.ComponentManager.Components.Add(camera);

        world.AddEntityImmediately(entity);

        //============ tiledMap ===============
        var tiledMapData = TiledMapLoader.LoadMapFromFile(@"Maps\map_1_1_tile_set.tiledMap");

        entity = new Entity();
        entity.Name = "TiledMap";
        entity.Coordinates.LocalPosition = new Vector3(0, 700, 0.0f);
        var tiledMapComponent = new TiledMapComponent(entity);
        tiledMapComponent.TiledMapData = tiledMapData;
        entity.ComponentManager.Components.Add(tiledMapComponent);

        world.AddEntityImmediately(entity);

        //============ common ressources ===============
        var sprites = SpriteLoader.LoadFromFile("Content\\TileSets\\RPG_sprites.spritesheet", GameManager.AssetContentManager, SaveOption.Editor);
        var animations = Animation2dLoader.LoadFromFile("Content\\TileSets\\RPG_animations.anims2d", GameManager.AssetContentManager);

        //============ sword ===============
        entity = new Entity();
        var weaponEntity = entity;
        entity.IsVisible = false;
        entity.IsEnabled = false;
        entity.Name = "link_sword";
        var animatedSprite = new AnimatedSpriteComponent(entity);
        entity.ComponentManager.Components.Add(animatedSprite);
        foreach (var animation in animations.Where(x => x.Name.StartsWith("baton")))
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }
        var gamePlayComponent = new GamePlayComponent(entity);
        entity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptPlayerWeapon(entity);

        world.AddEntityImmediately(entity);

        //============ player ===============
        entity = new Entity();
        entity.Name = "Link";
        entity.Coordinates.LocalPosition = new Vector3(60, 600, 0.3f);
        var physicsComponent = new Physics2dComponent(entity);
        entity.ComponentManager.Components.Add(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        physicsComponent.PhysicsDefinition.Mass = 1.0f;
        physicsComponent.Shape = new ShapeCircle(25);
        physicsComponent.PhysicsDefinition.ApplyGravity = false;
        physicsComponent.PhysicsDefinition.AngularFactor = Vector3.Zero;
        physicsComponent.PhysicsDefinition.Friction = 0f;
        physicsComponent.PhysicsDefinition.LinearSleepingThreshold = 0f;
        physicsComponent.PhysicsDefinition.AngularSleepingThreshold = 0f;
        animatedSprite = new AnimatedSpriteComponent(entity);
        entity.ComponentManager.Components.Add(animatedSprite);
        foreach (var animation in animations.Where(x => x.Name.StartsWith("swordman")))
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        var playerComponent = new PlayerComponent(entity);
        playerComponent.Character.AnimatationPrefix = "swordman";
        playerComponent.Character.SetWeapon(weaponEntity);
        entity.ComponentManager.Components.Add(playerComponent);

        world.AddEntityImmediately(entity);

        base.LoadContent();

        GameManager.ActiveCamera = camera;
    }


    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }


        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
    }
}
