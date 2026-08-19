using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scripting;

public abstract class GameplayProxy : ObjectBase, IGameplayProxy
{
    protected Entity Owner { get; private set; }

    public void Initialize(Entity owner)
    {
        Owner = owner;
        InitializePrivate();
    }

    public abstract void InitializeWithWorld(Scene.World.World world);

    public abstract void Update(float elapsedTime);
    public abstract void Draw();

    public abstract void OnHit(Collision collision);
    public abstract void OnHitEnded(Collision collision);
    public abstract void OnBeginPlay(Scene.World.World world);
    public abstract void OnEndPlay(Scene.World.World world);

    public abstract IGameplayProxy Clone();

    /// <summary>
    /// Restores the state written by <see cref="Save"/>. Overrides must call the base method.
    /// A state node written without the base keys is tolerated so a partial node
    /// never fails the entity or world load.
    /// </summary>
    public override void Load(JObject element)
    {
        if (element["id"] != null && element["name"] != null)
        {
            base.Load(element);
        }
    }

    /// <summary>
    /// Writes the state of the proxy into the "script" node of its entity or world document.
    /// Overrides must call the base method, which writes the keys <see cref="ObjectBase.Load"/> reads.
    /// </summary>
    public virtual void Save(JObject element)
    {
        element.Add("id", Id.ToString());
        element.Add("name", Name);
    }
}
