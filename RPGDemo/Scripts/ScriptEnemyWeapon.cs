using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using RPGDemo.Components;

namespace RPGDemo.Scripts;

public class ScriptEnemyWeapon : IExternalComponent
{
    private readonly Entity _entity;

    public ScriptEnemyWeapon(Entity entity)
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
        HitWithGrass(collision);
        HitWithPlayer(collision);
    }

    private void HitWithGrass(Collision collision)
    {
        TileCollisionManager tileCollisionManager = null;

        if (collision.ColliderA.Owner == _entity)
        {
            tileCollisionManager = collision.ColliderB as TileCollisionManager;
        }
        else if (collision.ColliderB.Owner == _entity)
        {
            tileCollisionManager = collision.ColliderA as TileCollisionManager;
        }

        if (tileCollisionManager != null)
        {
            //check if it AutoTile grass
            //tileCollisionManager.RemoveTile();
        }
    }

    private void HitWithPlayer(Collision collision)
    {
        PlayerComponent playerComponent = null;

        if (collision.ColliderA.Owner == _entity)
        {
            playerComponent = collision.ColliderB.Owner.ComponentManager.GetComponent<PlayerComponent>();
        }
        else if (collision.ColliderB.Owner == _entity)
        {
            playerComponent = collision.ColliderA.Owner.ComponentManager.GetComponent<PlayerComponent>();
        }

        if (playerComponent != null)
        {
            //check if it AutoTile grass
            //tileCollisionManager.RemoveTile();
        }
    }

    public void OnHitEnded(Collision collision)
    {
    }
}