using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Design;
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
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RPGDemo.Components;
using RPGDemo.Controllers;
using RPGDemo.Scripts;
using RPGDemo.Weapons;
using static Assimp.Metadata;

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
        GameSettings.AssetInfoManager.Load("Content\\AssetInfos.json", SaveOption.Editor);

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
        var tiledMapData = TileMapLoader.LoadMapFromFile(@"Maps\map_1_1.tileMap");

        entity = new Entity();
        entity.Name = "TileMap";
        entity.Coordinates.LocalPosition = new Vector3(0, 700, 0.0f);
        var tiledMapComponent = new TileMapComponent(entity);
        tiledMapComponent.TileMapData = tiledMapData;
        entity.ComponentManager.Components.Add(tiledMapComponent);

        world.AddEntityImmediately(entity);

        //============ common ressources ===============
        LoadSprites();
        var animations = LoadAnimations();

        var playerComponent = CreatePlayer(animations, world);
        CreateEnemy(animations, world, playerComponent);

        base.LoadContent();

        GameManager.ActiveCamera = camera;
    }

    private List<Animation2dData> LoadAnimations()
    {
        var animationsAssetInfos = GameSettings.AssetInfoManager.AssetInfos
            .Where(x => x.FileName.EndsWith(Constants.FileNameExtensions.Animation2d));

        var animations = new List<Animation2dData>();

        foreach (var assetInfo in animationsAssetInfos)
        {
            var animation2dData = GameManager.AssetContentManager.Load<Animation2dData>(assetInfo, GraphicsDevice);
            animations.Add(animation2dData);
        }

        return animations;
    }

    private void LoadSprites()
    {
        var spriteAssetInfos = GameSettings.AssetInfoManager.AssetInfos
            .Where(x => x.FileName.EndsWith(Constants.FileNameExtensions.Sprite));

        foreach (var assetInfo in spriteAssetInfos)
        {
            var spriteData = GameManager.AssetContentManager.Load<SpriteData>(assetInfo, GraphicsDevice);
        }
    }

    private static PlayerComponent CreatePlayer(List<Animation2dData> animations, World world)
    {
        //============ sword ===============
        var entity = new Entity();
        var weaponEntity = entity;
        entity.IsVisible = false;
        entity.IsEnabled = false;
        entity.Name = "link_sword";
        var animatedSprite = new AnimatedSpriteComponent(entity);
        entity.ComponentManager.Components.Add(animatedSprite);
        foreach (var animation in animations.Where(x => x.AssetInfo.Name.StartsWith("baton")))
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
        foreach (var animation in animations.Where(x => x.AssetInfo.Name.StartsWith("swordman")))
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }

        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        var playerComponent = new PlayerComponent(entity);
        playerComponent.Character.AnimatationPrefix = "swordman";
        playerComponent.Character.SetWeapon(new MeleeWeapon(weaponEntity));
        entity.ComponentManager.Components.Add(playerComponent);

        world.AddEntityImmediately(entity);

        return playerComponent;
    }

    private static void CreateEnemy(List<Animation2dData> animations, World world, PlayerComponent playerComponent)
    {
        //============ weapon rock ===============
        var entity = new Entity();
        var rockEntity = entity;
        entity.IsVisible = false;
        entity.IsEnabled = false;
        entity.Name = "rock";
        var physicsComponent = new Physics2dComponent(entity);
        entity.ComponentManager.Components.Add(physicsComponent);
        physicsComponent.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        physicsComponent.PhysicsDefinition.Mass = 1.0f;
        physicsComponent.Shape = new ShapeCircle(20);
        physicsComponent.PhysicsDefinition.ApplyGravity = false;
        physicsComponent.PhysicsDefinition.AngularFactor = Vector3.Zero;
        physicsComponent.PhysicsDefinition.Friction = 0f;
        physicsComponent.PhysicsDefinition.LinearSleepingThreshold = 0f;
        physicsComponent.PhysicsDefinition.AngularSleepingThreshold = 0f;
        var animatedSprite = new AnimatedSpriteComponent(entity);
        entity.ComponentManager.Components.Add(animatedSprite);
        foreach (var animation in animations.Where(x => x.AssetInfo.Name.StartsWith("rock")))
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }

        var gamePlayComponent = new GamePlayComponent(entity);
        entity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptEnemyWeapon(entity);

        world.AddEntityImmediately(entity);

        //============ enemy ===============
        entity = new Entity();
        entity.Name = "Octopus";
        entity.Coordinates.LocalPosition = new Vector3(600, 600, 0.3f);
        physicsComponent = new Physics2dComponent(entity);
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
        foreach (var animation in animations.Where(x => x.AssetInfo.Name.StartsWith("octopus")))
        {
            animatedSprite.AddAnimation(new Animation2d(animation));
        }

        animatedSprite.SetCurrentAnimation("octopus_stand_right", true);

        var enemyComponent = new EnemyComponent(entity);
        enemyComponent.Character.AnimatationPrefix = "octopus";
        enemyComponent.Character.SetWeapon(new ThrowableWeapon(rockEntity));
        entity.ComponentManager.Components.Add(enemyComponent);

        enemyComponent.Controller.PlayerHunted = playerComponent.Character;

        world.AddEntityImmediately(entity);
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
