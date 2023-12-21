using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptWorld : ExternalComponent
{
    public override int ExternalComponentId => (int)RpgDemoScriptIds.World;

    public override void Initialize(EntityBase entityBase)
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
        var camera = world.Entities.First(x => x.AssetInfo.Name == "camera");
        var camera3dIn2dAxisComponent = camera.ComponentManager.GetComponent<Camera3dIn2dAxisComponent>();
        camera3dIn2dAxisComponent.Target = new Vector3(world.Game.Window.ClientBounds.Size.X / 2f, world.Game.Window.ClientBounds.Size.Y / 2f, 0.0f);

        //mainHUD
        var assetInfo = GameSettings.AssetInfoManager.GetByFileName("Screens\\MainHUD\\MainHUD.screen");
        var screen = world.Game.GameManager.AssetContentManager.Load<Screen>(assetInfo);
        screen.ExternalComponent = new ScriptMainHUDScreen();
        screen.Initialize(world.Game);
        world.AddScreen(screen);
    }

    public override void OnEndPlay(World world)
    {

    }

    public override void Load(JsonElement element, SaveOption option)
    {
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}