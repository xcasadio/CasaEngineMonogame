using System;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptEnemySpawner : GameplayProxy
{
    private World _world;

    public override void InitializeWithWorld(World world)
    {
        _world = world;
    }

    public override void Update(float elapsedTime)
    {
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
    {
        _world = world;
        var spawnEntity = world.SpawnEntity<Pawn>(Guid.Parse("afd86c38-7a42-40e6-92b8-58b0d2def9d7")); //"Entities\\character_octopus");

        spawnEntity.RootComponent.Coordinates.Position = Owner.RootComponent.Position;
        spawnEntity.RootComponent.Coordinates.Orientation = Owner.RootComponent.Orientation;
    }

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptWorld Clone()
    {
        return new ScriptWorld();
    }
}