using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Physics;

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
}
