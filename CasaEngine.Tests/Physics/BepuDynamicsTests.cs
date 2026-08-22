using System.Collections.Generic;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Physics.Bepu;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the findings deferred from the Tranche 1 verification (see
/// ai-agent/tasks/bepu-physics-migration-tasks.md, Tranche 2): dynamic sleep with a real
/// <see cref="PhysicsDefinition.SleepThreshold"/>, a static's <c>RefreshBodyAabb</c> waking a
/// sleeping dynamic resting on it, and composite inertia for a dynamic compound.
/// </summary>
public class BepuDynamicsTests
{
    [Fact]
    public void Planar2dStack_FiveBoxes_StayOnThePlaneAndStayStanding()
    {
        using var physicsWorldContext = new PhysicsWorld(false);

        var floorDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Static };
        Matrix floorMatrix = Matrix.CreateTranslation(0f, -0.5f, 0f);
        using var floor = physicsWorldContext.AddStaticObject(
            new Box { Size = new Vector3(20f, 1f, 20f) },
            Vector3.One,
            ref floorMatrix,
            new object(),
            floorDefinition,
            CollisionProfileIds.WorldStatic);

        var boxes = new PhysicsBody[5];
        for (int i = 0; i < boxes.Length; i++)
        {
            var definition = new PhysicsDefinition
            {
                PhysicsType = PhysicsType.Dynamic,
                Mass = 1f,
                ApplyGravity = true,
                Friction = 0.8f,
                LinearFactor = new Vector3(1f, 1f, 0f),
                AngularFactor = new Vector3(0f, 0f, 1f),
            };

            Matrix worldMatrix = Matrix.CreateTranslation(0f, 0.5f + i, 0f);
            boxes[i] = physicsWorldContext.AddRigidBody(
                new Box(),
                Vector3.One,
                ref worldMatrix,
                new object(),
                definition,
                CollisionProfileIds.WorldDynamic);
        }

        for (int step = 0; step < 300; step++)
        {
            physicsWorldContext.Update(1f / 60f);
        }

        float previousY = 0f;
        for (int i = 0; i < boxes.Length; i++)
        {
            var translation = boxes[i].WorldTransform.Translation;
            Assert.True(Math.Abs(translation.Z) < 1e-3f, $"box {i} drifted on Z: {translation.Z}");
            Assert.True(translation.Y > previousY, $"box {i} is not above box {i - 1}: {translation.Y} <= {previousY}");
            previousY = translation.Y;
        }

        foreach (var box in boxes)
        {
            box.Dispose();
        }
    }

    [Fact]
    public void StillDynamicBody_FallsAsleepAfterEnoughSteps()
    {
        using var physicsWorldContext = new PhysicsWorld(false);

        var definition = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Dynamic,
            Mass = 1f,
            ApplyGravity = false,
            SleepThreshold = 0.01f,
        };

        Matrix worldMatrix = Matrix.Identity;
        using var body = physicsWorldContext.AddRigidBody(
            new Box(),
            Vector3.One,
            ref worldMatrix,
            new object(),
            definition,
            CollisionProfileIds.WorldDynamic);

        var backend = Assert.IsType<BepuBodyBackend>(body.Backend);
        Assert.True(backend.IsAwake);

        for (int step = 0; step < 120; step++)
        {
            physicsWorldContext.Update(1f / 60f);
        }

        Assert.False(backend.IsAwake);
    }

    [Fact]
    public void SleepingDynamicRestingOnAMovedStatic_WakesUpAndFalls()
    {
        using var physicsWorldContext = new PhysicsWorld(false);

        var floorDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Static };
        Matrix floorMatrix = Matrix.CreateTranslation(0f, -0.5f, 0f);
        using var floor = physicsWorldContext.AddStaticObject(
            new Box { Size = new Vector3(20f, 1f, 20f) },
            Vector3.One,
            ref floorMatrix,
            new object(),
            floorDefinition,
            CollisionProfileIds.WorldStatic);

        var boxDefinition = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Dynamic,
            Mass = 1f,
            ApplyGravity = true,
            Friction = 0.8f,
            SleepThreshold = 0.01f,
        };

        Matrix boxMatrix = Matrix.CreateTranslation(0f, 0.5f, 0f);
        using var box = physicsWorldContext.AddRigidBody(
            new Box(),
            Vector3.One,
            ref boxMatrix,
            new object(),
            boxDefinition,
            CollisionProfileIds.WorldDynamic);

        var backend = Assert.IsType<BepuBodyBackend>(box.Backend);

        for (int step = 0; step < 300; step++)
        {
            physicsWorldContext.Update(1f / 60f);
        }

        Assert.False(backend.IsAwake);
        float restingY = box.WorldTransform.Translation.Y;

        //Pull the floor away from underneath the sleeping box.
        floor.WorldTransform = Matrix.CreateTranslation(0f, -50f, 0f);
        physicsWorldContext.RefreshBodyAabb(floor);

        Assert.True(backend.IsAwake);

        for (int step = 0; step < 30; step++)
        {
            physicsWorldContext.Update(1f / 60f);
        }

        Assert.True(box.WorldTransform.Translation.Y < restingY - 0.1f);
    }

    [Fact]
    public void DynamicCompound_OfTwoOffsetBoxes_HasADifferentInertiaThanASingleBox()
    {
        using var physicsWorldContext = new PhysicsWorld(false);

        var singleDefinition = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Dynamic,
            Mass = 2f,
            ApplyGravity = false,
        };
        Matrix singleMatrix = Matrix.CreateTranslation(0f, 0f, 0f);
        using var singleBox = physicsWorldContext.AddRigidBody(
            new Box(),
            Vector3.One,
            ref singleMatrix,
            new object(),
            singleDefinition,
            CollisionProfileIds.WorldDynamic);

        var compoundFixtures = new[]
        {
            new ColliderFixture(new Box()) { LocalPosition = new Vector3(1f, 0f, 0f) },
            new ColliderFixture(new Box()) { LocalPosition = new Vector3(-1f, 0f, 0f) },
        };

        var compoundDefinition = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Dynamic,
            Mass = 2f,
            ApplyGravity = false,
        };
        Matrix compoundMatrix = Matrix.CreateTranslation(10f, 0f, 0f);
        using var compoundBox = physicsWorldContext.AddRigidBody(
            compoundFixtures,
            Vector3.One,
            ref compoundMatrix,
            new object(),
            compoundDefinition,
            CollisionProfileIds.WorldDynamic);

        Assert.True(compoundBox.IsCompound);
        Assert.False(singleBox.IsCompound);

        var singleBackend = Assert.IsType<BepuBodyBackend>(singleBox.Backend);
        var compoundBackend = Assert.IsType<BepuBodyBackend>(compoundBox.Backend);

        var singleTensor = singleBackend.Inertia.InverseInertiaTensor;
        var compoundTensor = compoundBackend.Inertia.InverseInertiaTensor;

        //Same total mass, same shapes, but the compound's children are offset from the body origin:
        //rotation about Y or Z (perpendicular to the X offset) is far harder than for a single box
        //centered on the origin, so its inverse inertia along those axes must be noticeably smaller.
        Assert.True(compoundTensor.YY < singleTensor.YY * 0.5f,
            $"compound YY ({compoundTensor.YY}) should be much smaller than single-box YY ({singleTensor.YY})");
        Assert.True(compoundTensor.ZZ < singleTensor.ZZ * 0.5f,
            $"compound ZZ ({compoundTensor.ZZ}) should be much smaller than single-box ZZ ({singleTensor.ZZ})");

        //Apply the same off-center impulse (about the Y axis) to both and let one substep run: the
        //compound, being harder to rotate about Y, must end up spinning slower than the single box.
        singleBox.ApplyImpulse(new Vector3(0f, 0f, 5f), new Vector3(0.5f, 0f, 0f));
        compoundBox.ApplyImpulse(new Vector3(0f, 0f, 5f), new Vector3(0.5f, 0f, 0f));

        physicsWorldContext.Update(1f / 60f);

        var singleAngularSpeed = ExtractYAngularSpeed(singleBox.WorldTransform, singleMatrix);
        var compoundAngularSpeed = ExtractYAngularSpeed(compoundBox.WorldTransform, compoundMatrix);

        Assert.True(compoundAngularSpeed < singleAngularSpeed,
            $"compound should spin slower about Y than the single box: compound={compoundAngularSpeed}, single={singleAngularSpeed}");
    }

    [Fact]
    public void ContinuousCollisionDetectionFlag_DrivesDynamicContinuity_GhostStaysPassive()
    {
        var originalFlags = GameSettings.PhysicsEngineSettings.Flags;
        try
        {
            GameSettings.PhysicsEngineSettings.Flags = PhysicsEngineFlags.ContinuousCollisionDetection;
            using (var physicsWorldContext = new PhysicsWorld(false))
            {
                var dynamicDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Dynamic, Mass = 1f };
                Matrix dynamicMatrix = Matrix.Identity;
                using var dynamicBody = physicsWorldContext.AddRigidBody(
                    new Box(),
                    Vector3.One,
                    ref dynamicMatrix,
                    new object(),
                    dynamicDefinition,
                    CollisionProfileIds.WorldDynamic);
                var dynamicBackend = Assert.IsType<BepuBodyBackend>(dynamicBody.Backend);
                Assert.Equal(BepuPhysics.Collidables.ContinuousDetectionMode.Continuous, dynamicBackend.Continuity.Mode);

                var ghostComponent = new TestCollideableComponent();
                Matrix ghostMatrix = Matrix.Identity;
                using var ghostBody = physicsWorldContext.AddGhostObject(
                    new Box(),
                    Vector3.One,
                    ref ghostMatrix,
                    ghostComponent,
                    CollisionProfileIds.Trigger);
                var ghostBackend = Assert.IsType<BepuBodyBackend>(ghostBody.Backend);
                Assert.Equal(BepuPhysics.Collidables.ContinuousDetectionMode.Passive, ghostBackend.Continuity.Mode);
            }

            GameSettings.PhysicsEngineSettings.Flags = PhysicsEngineFlags.None;
            using (var physicsWorldContext = new PhysicsWorld(false))
            {
                var dynamicDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Dynamic, Mass = 1f };
                Matrix dynamicMatrix = Matrix.Identity;
                using var dynamicBody = physicsWorldContext.AddRigidBody(
                    new Box(),
                    Vector3.One,
                    ref dynamicMatrix,
                    new object(),
                    dynamicDefinition,
                    CollisionProfileIds.WorldDynamic);
                var dynamicBackend = Assert.IsType<BepuBodyBackend>(dynamicBody.Backend);
                Assert.Equal(BepuPhysics.Collidables.ContinuousDetectionMode.Passive, dynamicBackend.Continuity.Mode);

                var ghostComponent = new TestCollideableComponent();
                Matrix ghostMatrix = Matrix.Identity;
                using var ghostBody = physicsWorldContext.AddGhostObject(
                    new Box(),
                    Vector3.One,
                    ref ghostMatrix,
                    ghostComponent,
                    CollisionProfileIds.Trigger);
                var ghostBackend = Assert.IsType<BepuBodyBackend>(ghostBody.Backend);
                Assert.Equal(BepuPhysics.Collidables.ContinuousDetectionMode.Passive, ghostBackend.Continuity.Mode);
            }
        }
        finally
        {
            GameSettings.PhysicsEngineSettings.Flags = originalFlags;
        }
    }

    [Fact]
    public void DynamicCompound_WithZOnlyAngularFactor_HasZeroOffDiagonalInverseInertiaTerms()
    {
        using var physicsWorldContext = new PhysicsWorld(false);

        var compoundFixtures = new[]
        {
            new ColliderFixture(new Box()) { LocalPosition = new Vector3(1f, 0f, 0f) },
            new ColliderFixture(new Box()) { LocalPosition = new Vector3(-1f, 0f, 0f) },
        };

        var compoundDefinition = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Dynamic,
            Mass = 2f,
            ApplyGravity = false,
            AngularFactor = new Vector3(0f, 0f, 1f),
        };
        Matrix compoundMatrix = Matrix.Identity;
        using var compoundBox = physicsWorldContext.AddRigidBody(
            compoundFixtures,
            Vector3.One,
            ref compoundMatrix,
            new object(),
            compoundDefinition,
            CollisionProfileIds.WorldDynamic);

        var compoundBackend = Assert.IsType<BepuBodyBackend>(compoundBox.Backend);
        var inverseInertiaTensor = compoundBackend.Inertia.InverseInertiaTensor;

        Assert.Equal(0f, inverseInertiaTensor.YX);
        Assert.Equal(0f, inverseInertiaTensor.ZX);
        Assert.Equal(0f, inverseInertiaTensor.ZY);
    }

    private sealed class TestCollideableComponent : ICollideableComponent
    {
        public Entity Owner { get; } = new();

        public PhysicsType PhysicsType => PhysicsType.Kinetic;

        public HashSet<Collision> Collisions { get; } = [];
    }

    private static float ExtractYAngularSpeed(Matrix after, Matrix before)
    {
        var deltaRotation = Matrix.CreateFromQuaternion(Quaternion.Inverse(Quaternion.CreateFromRotationMatrix(before)))
                             * after;
        var quaternion = Quaternion.CreateFromRotationMatrix(deltaRotation);
        quaternion.Normalize();

        //Angle of the rotation quaternion, regardless of axis; both impulses only ever produce
        //rotation about Y here (impulse and offset both lie in the XZ plane), so this is a fair
        //proxy for "how far did it spin".
        float angle = 2f * (float)Math.Acos(Math.Clamp(Math.Abs(quaternion.W), -1f, 1f));
        return angle;
    }
}
