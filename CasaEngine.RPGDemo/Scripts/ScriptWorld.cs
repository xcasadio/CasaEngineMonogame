using System.Linq;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptWorld : ExternalComponent
{
    public override int ExternalComponentId => (int)RpgDemoScriptIds.World;

    public override void Initialize(Entity entity)
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