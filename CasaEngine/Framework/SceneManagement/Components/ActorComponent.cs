using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;
using TomShane.Neoforce.Controls;

namespace CasaEngine.Framework.SceneManagement.Components;

//Actor Components (class UActorComponent) are most useful for abstract behaviors such as movement, 
//inventory or attribute management, and other non-physical concepts.
//Actor Components do not have a transform, meaning they do not have any physical location or rotation in the world.
public abstract class ActorComponent : UObject
{
    public AActor? Owner { get; private set; }

    protected ActorComponent()
    {
        //Do nothing
    }

    protected ActorComponent(ActorComponent other)
    {
        Owner = other.Owner;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public virtual void InitializeWithWorld(World.World world)
    {
        //if initialized

        //register event in blueprint like tick or BeginPlay
    }

    public abstract ActorComponent Clone();

    public virtual void Attach(AActor actor)
    {
        Owner = actor;
    }

    public virtual void Detach()
    {
        Owner = null;
    }

    public virtual void Update(float elapsedTime)
    {

    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }

#if EDITOR

    public string? DisplayName => GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("type", GetType().Name);
    }

#endif
}