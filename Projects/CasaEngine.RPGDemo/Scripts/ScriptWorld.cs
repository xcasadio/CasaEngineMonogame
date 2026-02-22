using System.IO;
using System.Linq;
using CasaEngine.Engine;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using CasaEngine.RPGDemo.Scripts.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptWorld : GameplayProxy
{
    private Character _playerCharacter;
    private World     _world;
    private UIRoot?   _uiRoot;
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
            _uiRoot?.PushScreen(new GameOverScreen(() =>
            {
                _world.Game.GameManager.SetWorldToLoad("TitleScreenWorld.world");
            }));
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
        var camera3dIn2dAxisComponent = camera.GetComponent<Camera3dIn2dAxisComponent>();
        camera3dIn2dAxisComponent.Target = new Vector3(
            world.Game.Window.ClientBounds.Size.X / 2f,
            world.Game.Window.ClientBounds.Size.Y / 2f,
            0.0f);

        // Get player character reference
        var entity       = world.Game.GameManager.CurrentWorld.Entities.First(x => x.Name == "character_link");
        var scriptPlayer = entity.GameplayProxy as ScriptPlayer;
        _playerCharacter = scriptPlayer.Character;

        // Get UIRoot for this view
        _uiRoot = world.Game.GameManager.ViewManager.Views
            .FirstOrDefault(v => v.UIRoot != null)?.UIRoot;
        if (_uiRoot == null) return;

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

        _uiRoot.PushScreen(new MainHUDScreen(portrait, GetHPPercent));
    }

    public override void OnEndPlay(World world)
    {
        _uiRoot?.ScreenStack.Clear();
        _uiRoot = null;
    }

    public override IGameplayProxy Clone()
    {
        return new ScriptWorld();
    }
}