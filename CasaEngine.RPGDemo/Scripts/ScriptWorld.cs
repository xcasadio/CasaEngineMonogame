using System.Linq;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptWorld : GameplayProxy
{
    public override void InitializeWithWorld(World world)
    {

    }

    public override void Update(float elapsedTime)
    {
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
    }

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptWorld Clone()
    {
        return new ScriptWorld();
    }
}