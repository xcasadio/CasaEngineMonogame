using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

//Actor Components (class UActorComponent) are most useful for abstract behaviors such as movement, 
//inventory or attribute management, and other non-physical concepts.
//Actor Components do not have a transform, meaning they do not have any physical location or rotation in the world.
public abstract class ActorComponent : UObject
{
    public AActor? Owner { get; internal set; }

    public World.World World { get; private set; }

    protected ActorComponent()
    {
    }

    protected ActorComponent(ActorComponent other)
    {
        World = other.World;
        Owner = other.Owner;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public virtual void InitializeWithWorld(World.World world)
    {
        World = world;

        //if initialized

        //register event in blueprint like tick or BeginPlay
    }

    public abstract ActorComponent Clone();

    public virtual void Update(float elapsedTime)
    {

    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("version", 1);
        jObject.Add("type", GetType().Name);
    }

#endif
}