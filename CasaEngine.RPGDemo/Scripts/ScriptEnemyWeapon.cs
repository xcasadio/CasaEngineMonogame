using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptEnemyWeapon : ExternalComponent
{
    public override int ExternalComponentId => (int)RpgDemoScriptIds.ThrowableWeapon;

    private Entity _entity;

    public ScriptEnemyWeapon()
    {
    }

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        _entity = entity;
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

        if (tileCollisionManager != null)
        {
            var tileData = tileCollisionManager.GetTileData();
            if (tileData.CollisionType == TileCollisionType.Blocked)
            {
                _entity.Destroy();
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

            var hitParameters = new HitParameters();
            hitParameters.ContactPoint = collision.ContactPoint;
            hitParameters.Entity = scriptPlayer.Character.Owner;
            hitParameters.Strength = 10;
            hitParameters.Precision = 10;
            hitParameters.MagicStrength = 10;
            scriptPlayer.Character.Hit(hitParameters);
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

public class HitParameters
{
    public int Strength;
    public int Precision;
    public int MagicStrength;

    public Entity Entity;

    public Vector3 ContactPoint;
}