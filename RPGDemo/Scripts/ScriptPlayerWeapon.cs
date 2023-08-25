using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using RPGDemo.Components;

namespace RPGDemo.Scripts;

public class ScriptPlayerWeapon : ExternalComponent
{
    private readonly Entity _entity;

    public ScriptPlayerWeapon(Entity entity)
    {
        _entity = entity;
    }

    public int Id => (int)ScriptIds.Custom + (int)ScriptRPGDemoIds.PlayerWeapon;

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
        HitWithEnemy(collision);
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

    private void HitWithEnemy(Collision collision)
    {
        EnemyComponent enemyComponent = null;

        if (collision.ColliderA.Owner == _entity)
        {
            enemyComponent = collision.ColliderB.Owner.ComponentManager.GetComponent<EnemyComponent>();
        }
        else if (collision.ColliderB.Owner == _entity)
        {
            enemyComponent = collision.ColliderA.Owner.ComponentManager.GetComponent<EnemyComponent>();
        }

        if (enemyComponent != null)
        {
            //check if it AutoTile grass
            //tileCollisionManager.RemoveTile();
        }
    }

    public void OnHitEnded(Collision collision)
    {
    }
}