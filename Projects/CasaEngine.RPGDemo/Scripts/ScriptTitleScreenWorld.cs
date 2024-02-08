using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptTitleScreenWorld : GameplayProxy
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
        //title screen
        var assetInfo = AssetCatalog.GetByFileName("Screens\\TitleScreen\\TitleScreen.screen");
        var screen = world.Game.AssetContentManager.Load<ScreenGui>(assetInfo.Id);
        screen.GameplayProxyClassName = nameof(ScriptTitleScreen);
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