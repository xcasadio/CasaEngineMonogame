using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;

namespace RPGDemo.Scripts;

public class ScriptPlayerWeapon : IExternalComponent
{
    private readonly Entity _entity;

    public ScriptPlayerWeapon(Entity entity)
    {
        _entity = entity;
    }

    public string Name { get; }
    public int Id { get; }

    public void Initialize(CasaEngineGame game)
    {
    }

    public void Update(float elapsedTime)
    {
    }

    public void Draw()
    {
    }

    public void OnHit(Collision collision)
    {
        TileCollisionManager tileCollisionManager = null;

        if (collision.ColliderA.Owner == _entity)
        {
            tileCollisionManager = collision.ColliderB as TileCollisionManager;
        }
        else if (collision.ColliderB.Owner == _entity)
        {
            tileCollisionManager = collision.ColliderB as TileCollisionManager;
        }

        if (tileCollisionManager != null)
        {
            //check if it AutoTile grass
            //tileCollisionManager.RemoveTile();
        }
    }

    public void OnHitEnded(Collision collision)
    {
    }
}