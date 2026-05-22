using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

//Entity Components (class EntityComponent) are most useful for abstract behaviors such as movement, 
//inventory or attribute management, and other non-physical concepts.
//Entity Components do not have a transform, meaning they do not have any physical location or rotation in the world.
public abstract class EntityComponent : ObjectBase
{
    public Entity? Owner { get; private set; }

    protected EntityComponent()
    {
        //Do nothing
    }

    protected EntityComponent(EntityComponent other)
    {
        Owner = other.Owner;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public virtual void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        //if initialized

        //register event in blueprint like tick or BeginPlay
    }

    public virtual void OnEnabledValueChange()
    {
        //Do nothing
    }

    public abstract EntityComponent Clone();

    public virtual void Attach(Entity actor)
    {
        Owner = actor;
    }

    public virtual void Detach()
    {
        Owner?.World?.CoroutineManager.StopAllCoroutines(this);
        Owner = null;
    }

    public virtual void Update(float elapsedTime)
    {

    }

    public override void Load(JObject element)
    {
        base.Load(element);
    }

    public string? DisplayName => GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
}