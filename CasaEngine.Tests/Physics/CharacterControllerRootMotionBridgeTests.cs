using CasaEngine.Core.Time;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public sealed class CharacterControllerRootMotionBridgeTests
{
    [Fact]
    public void Update_ConsumesRootMotionAndMovesThroughController()
    {
        Entity entity = CreateEntity(out CharacterControllerComponent controller, out FakeRootMotionSourceComponent source, out CharacterControllerRootMotionBridgeComponent bridge);
        source.NextDelta = new RootMotionDelta(new Vector3(1.5f, 0f, 0f), Quaternion.Identity);

        bridge.Update(0.1f);

        Assert.Equal(1, source.ConsumeCount);
        Assert.Equal(RootMotionMode.Apply, source.RootMotionMode);
        Assert.Equal(new Vector3(1.5f, 0f, 0f), bridge.LastConsumedRootMotionDelta.Translation);
        Assert.Equal(new Vector3(1.5f, 0f, 0f), bridge.LastAppliedDisplacement);
        Assert.Equal(new Vector3(1.5f, 0f, 0f), controller.LastActualDisplacement);
        Assert.Equal(1.5f, entity.RootComponent!.Position.X, precision: 5);
    }

    [Fact]
    public void ApplyRootMotionDelta_CanApplyRotationWhenEnabled()
    {
        Entity entity = CreateEntity(out _, out _, out CharacterControllerRootMotionBridgeComponent bridge);
        bridge.ApplyRotation = true;
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.PiOver2);

        bridge.ApplyRootMotionDelta(new RootMotionDelta(Vector3.Zero, rotation));

        Assert.Equal(rotation.X, entity.RootComponent!.Orientation.X, precision: 5);
        Assert.Equal(rotation.Y, entity.RootComponent.Orientation.Y, precision: 5);
        Assert.Equal(rotation.Z, entity.RootComponent.Orientation.Z, precision: 5);
        Assert.Equal(rotation.W, entity.RootComponent.Orientation.W, precision: 5);
    }

    [Fact]
    public void WorldSystem_ConsumesRootMotionRegardlessOfComponentInsertionOrder()
    {
        var world = new World();
        var entity = new Entity
        {
            RootComponent = new TestSceneComponent(),
        };
        var bridge = new CharacterControllerRootMotionBridgeComponent();
        var source = new FakeRootMotionSourceComponent();
        var controller = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                Gravity = 0f,
            }
        };
        entity.AddComponent(bridge);
        entity.AddComponent(source);
        entity.AddComponent(controller);
        AddEntityToWorld(world, entity);
        source.NextDelta = new RootMotionDelta(new Vector3(1.5f, 0f, 0f), Quaternion.Identity);

        world.Update(FrameTime.FromElapsedTime(0.1f, 1));

        Assert.Equal(1, source.ConsumeCount);
        Assert.Equal(new Vector3(1.5f, 0f, 0f), bridge.LastAppliedDisplacement);
        Assert.Equal(1.5f, entity.RootComponent!.Position.X, precision: 5);
    }

    private static Entity CreateEntity(
        out CharacterControllerComponent controller,
        out FakeRootMotionSourceComponent source,
        out CharacterControllerRootMotionBridgeComponent bridge)
    {
        var entity = new Entity
        {
            RootComponent = new TestSceneComponent(),
        };

        controller = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                Gravity = 0f,
            }
        };
        source = new FakeRootMotionSourceComponent();
        bridge = new CharacterControllerRootMotionBridgeComponent();
        entity.AddComponent(controller);
        entity.AddComponent(source);
        entity.AddComponent(bridge);
        return entity;
    }

    private sealed class FakeRootMotionSourceComponent : EntityComponent, IRootMotionDeltaSource
    {
        public RootMotionMode RootMotionMode { get; set; } = RootMotionMode.Observe;

        public RootMotionDelta NextDelta { get; set; } = RootMotionDelta.Identity;

        public int ConsumeCount { get; private set; }

        public RootMotionDelta ConsumeRootMotionDelta()
        {
            ConsumeCount++;
            RootMotionDelta delta = NextDelta;
            NextDelta = RootMotionDelta.Identity;
            return delta;
        }

        public override EntityComponent Clone()
        {
            return new FakeRootMotionSourceComponent
            {
                RootMotionMode = RootMotionMode,
                NextDelta = NextDelta,
            };
        }
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }

    private static void AddEntityToWorld(World world, Entity entity)
    {
        world.AddEntity(entity);
        world.Update(FrameTime.FromElapsedTime(0f));
    }
}