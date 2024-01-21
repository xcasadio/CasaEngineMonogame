using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptPlayerWeapon : GameplayProxy
{
    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        Owner.IsEnabled = false;
        Owner.IsVisible = false;
    }

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
        HitWithGrass(collision);
        HitWithEnemy(collision);
    }

    private void HitWithGrass(Collision collision)
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

        if (tileCollisionManager != null)
        {
            var tileData = tileCollisionManager.GetTileData();
            if (tileData.IsBreakable)
            {
                tileCollisionManager.RemoveTile();
            }
        }
    }

    private void HitWithEnemy(Collision collision)
    {
        ScriptEnemy scriptEnemy = null;

        if (collision.ColliderA.Owner == Owner)
        {
            scriptEnemy = collision.ColliderB.Owner.GameplayProxy as ScriptEnemy;
        }
        else if (collision.ColliderB.Owner == Owner)
        {
            scriptEnemy = collision.ColliderA.Owner.GameplayProxy as ScriptEnemy;
        }

        if (scriptEnemy != null)
        {
            var hitParameters = new HitParameters();
            hitParameters.ContactPoint = collision.ContactPoint;
            hitParameters.Entity = scriptEnemy.Character.Owner;
            hitParameters.Strength = 10;
            hitParameters.Precision = 10;
            hitParameters.MagicStrength = 10;
            scriptEnemy.Character.Hit(hitParameters);
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

    public override void Load(JsonElement element)
    {

    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}