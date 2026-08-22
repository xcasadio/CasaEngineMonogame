using System.Collections.Generic;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the Bepu debug renderer (Tranche 3): wireframe line counts per shape kind, including a
/// compound recursing into its children, and that <see cref="PhysicsDebugDrawModes.NoDebug"/> draws
/// nothing.
/// </summary>
public class BepuDebugDrawTests
{
    private const int BoxLineCount = 12;
    private const int SphereLineCount = 48;

    private sealed class CountingDebugDrawer : IPhysicsDebugDrawer
    {
        public PhysicsDebugDrawModes DebugMode { get; set; }

        public int LineCount { get; private set; }

        public void Draw3dText(ref Vector3 location, string textString)
        {
        }

        public void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color)
        {
        }

        public void DrawLine(ref Vector3 from, ref Vector3 to, Color color)
        {
            LineCount++;
        }

        public void ReportErrorWarning(string warningString)
        {
        }
    }

    private static PhysicsWorld BuildWorldWithOneOfEachShape(out int expectedLineCount)
    {
        var physicsWorld = new PhysicsWorld(false);

        var staticDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Static };
        var staticMatrix = Matrix.Identity;
        physicsWorld.AddStaticObject(new Box(), Vector3.One, ref staticMatrix, new object(), staticDefinition, CollisionProfileIds.WorldStatic);

        var dynamicDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Dynamic, Mass = 1f };
        var dynamicMatrix = Matrix.CreateTranslation(0f, 5f, 0f);
        physicsWorld.AddRigidBody(new Sphere(), Vector3.One, ref dynamicMatrix, new object(), dynamicDefinition, CollisionProfileIds.WorldDynamic);

        var fixtures = new List<ColliderFixture>
        {
            new ColliderFixture(new Box()) { Tag = "a" },
            new ColliderFixture(new Box()) { LocalPosition = new Vector3(1f, 0f, 0f), Tag = "b" }
        };
        var kinematicDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Kinetic };
        var kinematicMatrix = Matrix.CreateTranslation(0f, 10f, 0f);
        physicsWorld.AddRigidBody(fixtures, Vector3.One, ref kinematicMatrix, new object(), kinematicDefinition, CollisionProfileIds.Pawn);

        //Static box (12) + dynamic sphere (3 great circles x 16 segments = 48) + kinematic compound
        //of 2 boxes (12 each, recursed).
        expectedLineCount = BoxLineCount + SphereLineCount + 2 * BoxLineCount;
        return physicsWorld;
    }

    [Fact]
    public void DrawDebugWorld_Wireframe_DrawsExpectedLineCountPerShape()
    {
        using var physicsWorld = BuildWorldWithOneOfEachShape(out var expectedLineCount);

        var drawer = new CountingDebugDrawer { DebugMode = PhysicsDebugDrawModes.DrawWireframe };
        physicsWorld.DrawDebugWorld(drawer);

        Assert.Equal(expectedLineCount, drawer.LineCount);
    }

    [Fact]
    public void DrawDebugWorld_NoDebug_DrawsNoLines()
    {
        using var physicsWorld = BuildWorldWithOneOfEachShape(out _);

        var drawer = new CountingDebugDrawer { DebugMode = PhysicsDebugDrawModes.NoDebug };
        physicsWorld.DrawDebugWorld(drawer);

        Assert.Equal(0, drawer.LineCount);
    }
}
