using System;
using System.IO;
using System.Linq;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Components;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Scripts;
using CasaEngine.RPGDemo.Weapons;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using static System.Net.Mime.MediaTypeNames;
using TomShane.Neoforce.Controls;

namespace CasaEngine.RPGDemo;

public class RpgGame : CasaEngineGame
{
    private static readonly float CharacterZOffSet = 0.3f;
    private SpriteFontBase _font;

    private Character _playerCharacter;
    private Character _enemyCharacter;

    public RpgGame() : base("Content\\RPGDemo.json")
    {

    }

    protected override void Initialize()
    {
        EngineEnvironment.ProjectPath = Path.Combine(Environment.CurrentDirectory, "Content");
        base.Initialize();

        //GameManager.SpriteRendererComponent.IsDrawCollisionsEnabled = true;
        //GameManager.SpriteRendererComponent.IsDrawSpriteOriginEnabled = true;

        //PhysicsDebugViewRendererComponent.DisplayPhysics = true;

        LogManager.Instance.AddLogger(new DebugLogger());
        LogManager.Instance.AddLogger(new FileLogger("log.txt"));
        LogManager.Instance.Verbosity = LogVerbosity.Trace;
    }

    protected override void LoadContent()
    {
        GameManager.DefaultSpriteFont = Content.Load<SpriteFont>("GizmoFont");

        //TODO : find a way to load the very first world just before LoadContent and Initialize it after this call
        var world = new World();
        var fileName = Path.Combine(EngineEnvironment.ProjectPath, GameSettings.ProjectSettings.FirstWorldLoaded);
        world.Load(fileName, SaveOption.Editor);
        GameManager.CurrentWorld = world;

        //============ Camera ===============
        var entity = new Entity();
        var camera = new Camera3dIn2dAxisComponent(entity);
        camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.ComponentManager.Components.Add(camera);

        world.AddEntityImmediately(entity);

        GameManager.FontSystem.AddFont(File.ReadAllBytes(@"C:\\Windows\\Fonts\\Tahoma.ttf"));
        _font = GameManager.FontSystem.GetFont(20);

        base.LoadContent();

        var playerComponent = InitializePlayer(world);
        var enemyComponent = InitializeEnemy(world, playerComponent);

        _playerCharacter = playerComponent.Character;
        _enemyCharacter = enemyComponent.Character;

        GameManager.ActiveCamera = camera;
    }

    private PlayerComponent InitializePlayer(World world)
    {
        var entity = world.Entities.First(x => x.Name == "character_link");
        var animatedSprite = entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        var playerComponent = new PlayerComponent(entity);
        playerComponent.Character.AnimatationPrefix = "swordman";
        var weaponEntity = GameManager.SpawnEntity("weapon_sword");
        weaponEntity.Initialize(this);
        playerComponent.Character.SetWeapon(new MeleeWeapon(this, weaponEntity));
        entity.ComponentManager.Components.Add(playerComponent);

        playerComponent.Initialize(this);

        //weapon
        weaponEntity.IsVisible = false;
        weaponEntity.IsEnabled = false;
        var gamePlayComponent = weaponEntity.ComponentManager.GetComponent<GamePlayComponent>();
        weaponEntity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptPlayerWeapon(weaponEntity);

        return entity.ComponentManager.GetComponent<PlayerComponent>();
    }

    private EnemyComponent InitializeEnemy(World world, PlayerComponent playerComponent)
    {
        var entity = world.Entities.First(x => x.Name == "character_octopus");
        var animatedSprite = entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("octopus_stand_right", true);

        var enemyComponent = new EnemyComponent(entity);
        enemyComponent.Character.AnimatationPrefix = "octopus";
        enemyComponent.Character.SetWeapon(new ThrowableWeapon(this, "weapon_rock"));
        entity.ComponentManager.Components.Add(enemyComponent);

        enemyComponent.Initialize(this);

        (enemyComponent.Controller as EnemyController).PlayerHunted = playerComponent.Character;

        return enemyComponent;
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

        GameManager.SpriteBatch.Begin();
        _font.DrawText(GameManager.SpriteBatch, $"Player HP: {_playerCharacter.HP}/{_playerCharacter.HPMax}", new Vector2(10, 10), Color.White, layerDepth: 0f);
        _font.DrawText(GameManager.SpriteBatch, $"Enemy HP: {_enemyCharacter.HP}/{_enemyCharacter.HPMax}", new Vector2(10, 10 + _font.LineHeight), Color.White, layerDepth: 0f);
        GameManager.SpriteBatch.End();
    }
}
