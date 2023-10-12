using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptEnemyWeapon : ExternalComponent
{
    public override int ExternalComponentId => (int)RpgDemoScriptIds.ThrowableWeapon;

    private Entity _entity;

    public override void Initialize(Entity entity)
    {
        _entity = entity;
    }

    public override void Update(float elapsedTime)
    {
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
        HitWithMap(collision);
        HitWithPlayer(collision);
    }

    private void HitWithMap(Collision collision)
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

        if (tileCollisionManager != null) // && weapon IsBreakable ?
        {
            var tileData = tileCollisionManager.GetTileData();
            if (tileData.CollisionType == TileCollisionType.Blocked)
            {
                _entity.Destroy();

                //var entity = Game.GameManager.SpawnEntity("Break effect");
            }
        }
    }

    private void HitWithPlayer(Collision collision)
    {
        ScriptPlayer scriptPlayer = null;

        if (collision.ColliderA.Owner == _entity)
        {
            scriptPlayer = collision.ColliderB.Owner.ComponentManager.GetComponent<GamePlayComponent>()?.ExternalComponent as ScriptPlayer;
        }
        else if (collision.ColliderB.Owner == _entity)
        {
            scriptPlayer = collision.ColliderA.Owner.ComponentManager.GetComponent<GamePlayComponent>()?.ExternalComponent as ScriptPlayer;
        }

        if (scriptPlayer != null)
        {
            _entity.Destroy();

            var hitParameters = new HitParameters
            {
                ContactPoint = collision.ContactPoint,
                Entity = scriptPlayer.Character.Owner,
                Strength = 10,
                Precision = 10,
                MagicStrength = 10
            };
            scriptPlayer.Character.Hit(hitParameters);
        }
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
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