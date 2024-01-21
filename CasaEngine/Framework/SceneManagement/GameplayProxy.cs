using CasaEngine.Engine.Physics;

namespace CasaEngine.Framework.SceneManagement;

public abstract class GameplayProxy : UObject
{
    protected AActor Owner { get; private set; }

    public void Initialize(AActor owner)
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
}