using System;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.World;

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
        var spawnEntity = world.SpawnEntity<Entity>(Guid.Parse("afd86c38-7a42-40e6-92b8-58b0d2def9d7")); //"Entities\\character_octopus");

        spawnEntity.RootComponent.LocalTransform.Position = Owner.RootComponent.Position;
        spawnEntity.RootComponent.LocalTransform.Orientation = Owner.RootComponent.Orientation;
    }

    public override void OnEndPlay(World world)
    {

    }

    public override IGameplayProxy Clone()
    {
        return new ScriptWorld();
    }
}