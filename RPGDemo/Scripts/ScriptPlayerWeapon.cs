using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using RPGDemo.Components;

namespace RPGDemo.Scripts;

public class ScriptPlayerWeapon : ExternalComponent
{
    public static int ScriptId => (int)RpgDemoScriptIds.Player;

    private readonly Entity _entity;

    public ScriptPlayerWeapon(Entity entity) : base(ScriptId)
    {
        _entity = entity;
    }

    public override void Initialize(CasaEngineGame game)
    {
    }

    public override void Update(float elapsedTime)
    {
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
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

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void Load(JsonElement element, SaveOption option)
    {

    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}