using System.Linq;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptWorld : GameplayProxy
{
    private Character _playerCharacter;
    private World _world;

    public override void InitializeWithWorld(World world)
    {
        _world = world;
    }

    public override void Update(float elapsedTime)
    {
        if (_playerCharacter.IsDead)
        {
            _world.Game.GameManager.SetWorldToLoad("TitleScreenWorld.world");
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
        camera3dIn2dAxisComponent.Target = new Vector3(world.Game.Window.ClientBounds.Size.X / 2f, world.Game.Window.ClientBounds.Size.Y / 2f, 0.0f);

        //mainHUD
        var assetInfo = AssetCatalog.GetByFileName("Screens\\MainHUD\\MainHUD.screen");
        var screen = world.Game.AssetContentManager.Load<ScreenGui>(assetInfo.Id);
        screen.GameplayProxyClassName = nameof(ScriptMainHUDScreen);
        screen.Initialize();
        screen.InitializeWithWorld(world);
        world.AddScreen(screen);


        //TODO : put in gamemode
        var entity = world.Game.GameManager.CurrentWorld.Entities.First(x => x.Name == "character_link");
        var scriptPlayer = entity.GameplayProxy as ScriptPlayer;
        _playerCharacter = scriptPlayer.Character;
    }

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptWorld Clone()
    {
        return new ScriptWorld();
    }
}