using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using CasaEngine.RPGDemo.Components;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptEnemyWeapon : ExternalComponent
{
    public static int ScriptId => (int)RpgDemoScriptIds.Enemy;

    private readonly Entity _entity;

    public ScriptEnemyWeapon(Entity entity) : base(ScriptId)
    {
        _entity = entity;
    }

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        _entity.IsEnabled = false;
        _entity.IsVisible = false;
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
            _entity.Destroy();

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