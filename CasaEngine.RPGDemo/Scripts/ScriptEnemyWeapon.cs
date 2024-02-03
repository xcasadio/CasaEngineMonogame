using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptEnemyWeapon : GameplayProxy
{
    public override void InitializeWithWorld(World world)
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
        HitWithMap(collision);
        HitWithPlayer(collision);
    }

    private void HitWithMap(Collision collision)
    {
        TileCollisionManager tileCollisionManager = null;

        if (collision.ColliderA.Owner == Owner)
        {
            tileCollisionManager = collision.ColliderB as TileCollisionManager;
        }
        else if (collision.ColliderB.Owner == Owner)
        {
            tileCollisionManager = collision.ColliderA as TileCollisionManager;
        }

        if (tileCollisionManager != null) // && weapon IsBreakable ?
        {
            var tileData = tileCollisionManager.GetTileData();
            if (tileData.CollisionType == TileCollisionType.Blocked)
            {
                Owner.Destroy();

                //var entity = Game.GameManager.SpawnEntity("Break effect");
            }
        }
    }

    private void HitWithPlayer(Collision collision)
    {
        ScriptPlayer scriptPlayer = null;

        if (collision.ColliderA.Owner == Owner)
        {
            scriptPlayer = collision.ColliderB.Owner.GameplayProxy as ScriptPlayer;
        }
        else if (collision.ColliderB.Owner == Owner)
        {
            scriptPlayer = collision.ColliderA.Owner.GameplayProxy as ScriptPlayer;
        }

        if (scriptPlayer != null)
        {
            Owner.Destroy();

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

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptEnemyWeapon Clone()
    {
        return new ScriptEnemyWeapon();
    }
}