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
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Scripts;
using CasaEngine.RPGDemo.Weapons;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.RPGDemo;

public class RpgGame : CasaEngineGame
{
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
        var camera = new Camera3dIn2dAxisComponent();
        camera.Target = new Vector3(Window.ClientBounds.Size.X / 2f, Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.ComponentManager.Components.Add(camera);

        world.AddEntityImmediately(entity);

        GameManager.FontSystem.AddFont(File.ReadAllBytes(@"C:\\Windows\\Fonts\\Tahoma.ttf"));
        _font = GameManager.FontSystem.GetFont(20);

        base.LoadContent();

        _playerCharacter = InitializePlayer(world);
        _enemyCharacter = InitializeEnemy(world, _playerCharacter);

        GameManager.ActiveCamera = camera;
    }

    private Character InitializePlayer(World world)
    {
        var entity = world.Entities.First(x => x.Name == "character_link");
        var animatedSprite = entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("swordman_stand_right", true);

        var gamePlayComponent = entity.ComponentManager.GetComponent<GamePlayComponent>();
        var scriptPlayer = new ScriptPlayer();
        gamePlayComponent.ExternalComponent = scriptPlayer;
        scriptPlayer.Initialize(entity);
        scriptPlayer.Character.AnimatationPrefix = "swordman";
        var weaponEntity = GameManager.SpawnEntity("weapon_sword");
        scriptPlayer.Character.SetWeapon(new MeleeWeapon(this, weaponEntity));

        //weapon
        weaponEntity.IsVisible = false;
        weaponEntity.IsEnabled = false;
        gamePlayComponent = weaponEntity.ComponentManager.GetComponent<GamePlayComponent>();
        gamePlayComponent.ExternalComponent = new ScriptPlayerWeapon();
        weaponEntity.Initialize(this);

        return scriptPlayer.Character;
    }

    private Character InitializeEnemy(World world, Character playerCharacter)
    {
        var entity = world.Entities.First(x => x.Name == "character_octopus");
        var animatedSprite = entity.ComponentManager.GetComponent<AnimatedSpriteComponent>();
        animatedSprite.SetCurrentAnimation("octopus_stand_right", true);

        var gamePlayComponent = entity.ComponentManager.GetComponent<GamePlayComponent>();
        var scriptEnemy = new ScriptEnemy();
        gamePlayComponent.ExternalComponent = scriptEnemy;
        scriptEnemy.Initialize(entity);
        scriptEnemy.Character.AnimatationPrefix = "octopus";
        scriptEnemy.Character.SetWeapon(new ThrowableWeapon(this, "weapon_rock"));
        (scriptEnemy.Controller as EnemyController).PlayerHunted = playerCharacter;

        return scriptEnemy.Character;
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
