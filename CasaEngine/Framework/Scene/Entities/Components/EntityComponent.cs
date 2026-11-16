using System.Collections;
using System.ComponentModel;
using System.Reflection;
using CasaEngine.Framework.Scripting.Coroutines;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public interface IWorldSystemDrivenComponent
{
}

//Entity Components (class EntityComponent) are most useful for abstract behaviors such as movement, 
//inventory or attribute management, and other non-physical concepts.
//Entity Components do not have a transform, meaning they do not have any physical location or rotation in the world.
public abstract class EntityComponent : ObjectBase
{
    public Entity Owner { get; private set; }

    protected EntityComponent()
    {
        //Do nothing
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    protected EntityComponent(Guid id) : base(id)
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

    /// <summary>
    /// Clones the component and gives the clone the name of the original. Clone() overrides either go through
    /// a copy constructor, which names the clone "Object &lt;id&gt;", or build a new object; this keeps the name in both cases.
    /// The clone keeps the new id it received.
    /// </summary>
    internal EntityComponent CloneWithName()
    {
        var clone = Clone();
        clone.Name = Name;
        return clone;
    }

    public virtual void Attach(Entity actor)
    {
        Owner = actor;
    }

    public virtual void Detach()
    {
        Owner?.World?.RuntimeSystems.CoroutineManager.StopAllCoroutines(this);
        Owner = null;
    }

    protected CoroutineHandle StartCoroutine(IEnumerator routine)
    {
        return GetCoroutineManager().StartCoroutine(routine, this);
    }

    protected CoroutineHandle StartCoroutine(IEnumerator routine, string name)
    {
        return GetCoroutineManager().StartCoroutine(routine, this, name);
    }

    protected void StopCoroutine(CoroutineHandle handle)
    {
        Owner?.World?.RuntimeSystems.CoroutineManager.StopCoroutine(handle);
    }

    protected void StopAllCoroutines()
    {
        Owner?.World?.RuntimeSystems.CoroutineManager.StopAllCoroutines(this);
    }

    private CoroutineManager GetCoroutineManager()
    {
        var coroutineManager = Owner?.World?.RuntimeSystems.CoroutineManager;
        if (coroutineManager == null)
        {
            throw new InvalidOperationException($"Component '{Name}' is not attached to an Entity in a World.");
        }

        return coroutineManager;
    }

    public virtual void Update(float elapsedTime)
    {

    }

    public override void Load(JObject element)
    {
        base.Load(element);
    }

    public string DisplayName => GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
}