using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.Scripting;

public abstract class GameplayProxy : ObjectBase
{
    protected Entity Owner { get; private set; }

    public void Initialize(Entity owner)
    {
        Owner = owner;
        InitializePrivate();
    }

    public abstract void InitializeWithWorld(World.World world);

    public abstract void Update(float elapsedTime);
    public abstract void Draw();

    public abstract void OnHit(Collision collision);
    public abstract void OnHitEnded(Collision collision);
    public abstract void OnBeginPlay(World.World world);
    public abstract void OnEndPlay(World.World world);

    public abstract GameplayProxy Clone();
}
