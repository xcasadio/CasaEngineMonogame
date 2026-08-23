using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.Scene.World;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Scripts.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptWorld : GameplayProxy
{
    private Character _playerCharacter;
    private World     _world;
    private MainHUDScreen?  _mainHudScreen;
    private GameOverScreen? _gameOverScreen;
    private bool      _gameOverShown;

    public override void InitializeWithWorld(World world)
    {
        _world = world;
    }

    public override void Update(float elapsedTime)
    {
        if (_playerCharacter.IsDead && !_gameOverShown)
        {
            _gameOverShown = true;
            _gameOverScreen = new GameOverScreen(() =>
            {
                _world.Game.GameManager.SetWorldToLoad("TitleScreenWorld.world");
            });
            _world.Game.GameManager.ScreenManager.PushScreenToActiveView(_gameOverScreen);
        }
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
    {
        var camera = world.Entities.First(x => x.Name == "camera");
        var camera2dComponent = camera.GetComponent<Camera2dComponent>();
        camera2dComponent.Target = new Vector3(
            world.Game.Window.ClientBounds.Size.X / 2f,
            world.Game.Window.ClientBounds.Size.Y / 2f,
            0.0f);

        // Get player character reference
        var entity       = world.Game.GameManager.CurrentWorld.Entities.First(x => x.Name == "character_link");
        var scriptPlayer = entity.GameplayProxy as ScriptPlayer;
        _playerCharacter = scriptPlayer.Character;

        // Get UI view for this render view.
        if (world.Game.GameManager.ViewManager.GetActiveUIView() == null) return;

        // Load portrait texture (falls back gracefully if missing)
        Texture2D? portrait = null;
        try
        {
            var portraitPath = Path.Combine(EngineEnvironment.ProjectPath,
                "Screens", "MainHUD", "MainHUD.png");
            if (File.Exists(portraitPath))
                portrait = Texture2D.FromFile(world.Game.GraphicsDevice, portraitPath);
        }
        catch { /* portrait is optional */ }

        float GetHPPercent() => _playerCharacter.HPMax > 0
            ? ((float)_playerCharacter.HP / _playerCharacter.HPMax) * 100f
            : 0f;

        _mainHudScreen = new MainHUDScreen(portrait, GetHPPercent);
        world.Game.GameManager.ScreenManager.PushScreenToActiveView(_mainHudScreen);
    }

    public override void OnEndPlay(World world)
    {
        if (_gameOverScreen != null)
        {
            world.Game.GameManager.ScreenManager.RemoveScreenFromActiveView(_gameOverScreen);
        }

        if (_mainHudScreen != null)
        {
            world.Game.GameManager.ScreenManager.RemoveScreenFromActiveView(_mainHudScreen);
        }

        _gameOverScreen = null;
        _mainHudScreen = null;
    }

    public override IGameplayProxy Clone()
    {
        return new ScriptWorld();
    }
}