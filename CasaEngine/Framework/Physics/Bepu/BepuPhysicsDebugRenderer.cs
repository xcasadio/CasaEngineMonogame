using System;
using BepuPhysics;
using BepuPhysics.Collidables;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using BepuBox = BepuPhysics.Collidables.Box;
using BepuSphere = BepuPhysics.Collidables.Sphere;
using BepuCapsule = BepuPhysics.Collidables.Capsule;
using BepuCylinder = BepuPhysics.Collidables.Cylinder;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// Draws the wireframe, AABBs and contact points of a <see cref="BepuPhysicsEngine"/> simulation onto an
/// <see cref="IPhysicsDebugDrawer"/>. Bepu has no built-in debug draw (unlike Bullet's native
/// <c>DebugDrawWorld</c>), so this walks the simulation's raw body/static storage directly. Every vertex
/// table is precomputed once; drawing itself never allocates.
/// </summary>
internal static class BepuPhysicsDebugRenderer
{
    private const int CircleSegments = 16;
    private const float ContactTolerance = 0.02f;

    private static readonly Color AwakeDynamicColor = Color.Green;
    private static readonly Color StaticOrSleepingColor = Color.Gray;
    private static readonly Color SensorOrKinematicColor = Color.Blue;
    private static readonly Color ContactColor = Color.Yellow;
    private static readonly Color AabbColor = Color.Orange;

    /// <summary>Unit circle in the local XY plane, 0..2*pi over <see cref="CircleSegments"/> steps.
    /// Every ring/circle drawn below is this table read with two of its three axes remapped.</summary>
    private static readonly Vector3[] UnitCircle = BuildUnitCircle();

    private static Vector3[] BuildUnitCircle()
    {
        var points = new Vector3[CircleSegments];
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * MathHelper.TwoPi / CircleSegments;
            points[i] = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
        }

        return points;
    }

    internal static void Draw(BepuPhysicsEngine engine, IPhysicsDebugDrawer debugDrawer)
    {
        if (debugDrawer == null)
        {
            return;
        }

        var mode = debugDrawer.DebugMode;
        if (mode == PhysicsDebugDrawModes.NoDebug)
        {
            return;
        }

        bool drawWireframe = (mode & PhysicsDebugDrawModes.DrawWireframe) != 0;
        bool drawAabb = (mode & PhysicsDebugDrawModes.DrawAabb) != 0;
        bool drawContacts = (mode & PhysicsDebugDrawModes.DrawContactPoints) != 0;

        if (drawWireframe || drawAabb)
        {
            DrawBodies(engine, debugDrawer, drawWireframe, drawAabb);
            DrawStatics(engine, debugDrawer, drawWireframe, drawAabb);
        }

        if (drawContacts)
        {
            DrawContacts(engine, debugDrawer);
        }
    }

    // ----- Bodies (active set + sleeping sets) and statics ----------------------------------

    private static void DrawBodies(BepuPhysicsEngine engine, IPhysicsDebugDrawer debugDrawer, bool drawWireframe, bool drawAabb)
    {
        var simulation = engine.Simulation;
        var sets = simulation.Bodies.Sets;

        for (int setIndex = 0; setIndex < sets.Length; setIndex++)
        {
            ref var set = ref sets[setIndex];
            if (!set.Allocated)
            {
                continue;
            }

            //Set 0 is the active set: every body in it is awake. Sets 1+ are inactive (sleeping) islands.
            bool isAwake = setIndex == 0;

            for (int bodyIndex = 0; bodyIndex < set.Count; bodyIndex++)
            {
                var handle = set.IndexToHandle[bodyIndex];
                var backend = engine.GetBodyBackend(handle);
                var shapeIndex = set.Collidables[bodyIndex].Shape;
                var pose = set.DynamicsState[bodyIndex].Motion.Pose;
                var color = ResolveColor(engine, backend, isAwake);

                if (drawWireframe)
                {
                    DrawShape(simulation, debugDrawer, shapeIndex, pose, color);
                }

                if (drawAabb)
                {
                    DrawAabb(simulation, debugDrawer, shapeIndex, pose);
                }
            }
        }
    }

    private static void DrawStatics(BepuPhysicsEngine engine, IPhysicsDebugDrawer debugDrawer, bool drawWireframe, bool drawAabb)
    {
        var simulation = engine.Simulation;
        var statics = simulation.Statics;

        for (int i = 0; i < statics.Count; i++)
        {
            var handle = statics.IndexToHandle[i];
            var backend = engine.GetStaticBackend(handle);
            ref var staticData = ref statics[i];
            var color = ResolveColor(engine, backend, isAwake: false);

            if (drawWireframe)
            {
                DrawShape(simulation, debugDrawer, staticData.Shape, staticData.Pose, color);
            }

            if (drawAabb)
            {
                DrawAabb(simulation, debugDrawer, staticData.Shape, staticData.Pose);
            }
        }
    }

    private static Color ResolveColor(BepuPhysicsEngine engine, BepuBodyBackend backend, bool isAwake)
    {
        if (backend == null)
        {
            return isAwake ? AwakeDynamicColor : StaticOrSleepingColor;
        }

        if (backend.DebugColor.HasValue)
        {
            return backend.DebugColor.Value;
        }

        var profileColor = engine.CollisionProfiles.GetProfile(backend.CollisionProfile.Id).DebugColor;
        if (profileColor.HasValue)
        {
            return profileColor.Value;
        }

        if (!backend.HasContactResponse || backend.Mobility == BepuMobility.Kinematic)
        {
            return SensorOrKinematicColor;
        }

        if (backend.Mobility == BepuMobility.Static || !isAwake)
        {
            return StaticOrSleepingColor;
        }

        return AwakeDynamicColor;
    }

    // ----- Wireframe: box (12 edges), sphere (3x16 circle), capsule/cylinder (rings + verticals),
    // compound (recurse into children at their local pose composed with the parent pose) -------

    private static void DrawShape(Simulation simulation, IPhysicsDebugDrawer debugDrawer, TypedIndex shapeIndex, RigidPose pose, Color color)
    {
        int type = shapeIndex.Type;

        if (type == Compound.Id)
        {
            ref var compound = ref simulation.Shapes.GetShape<Compound>(shapeIndex.Index);
            for (int i = 0; i < compound.Children.Length; i++)
            {
                ref var child = ref compound.Children[i];
                RigidPose.MultiplyWithoutOverlap(new RigidPose(child.LocalPosition, child.LocalOrientation), pose, out var childPose);
                DrawShape(simulation, debugDrawer, child.ShapeIndex, childPose, color);
            }

            return;
        }

        var matrix = pose.ToMatrix();

        if (type == BepuBox.Id)
        {
            ref var box = ref simulation.Shapes.GetShape<BepuBox>(shapeIndex.Index);
            DrawBox(debugDrawer, matrix, box.HalfWidth, box.HalfHeight, box.HalfLength, color);
        }
        else if (type == BepuSphere.Id)
        {
            ref var sphere = ref simulation.Shapes.GetShape<BepuSphere>(shapeIndex.Index);
            DrawSphere(debugDrawer, matrix, sphere.Radius, color);
        }
        else if (type == BepuCapsule.Id)
        {
            ref var capsule = ref simulation.Shapes.GetShape<BepuCapsule>(shapeIndex.Index);
            DrawCapsule(debugDrawer, matrix, capsule.Radius, capsule.HalfLength, color);
        }
        else if (type == BepuCylinder.Id)
        {
            ref var cylinder = ref simulation.Shapes.GetShape<BepuCylinder>(shapeIndex.Index);
            DrawCylinder(debugDrawer, matrix, cylinder.Radius, cylinder.HalfLength, color);
        }
    }

    private static void DrawBox(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float hw, float hh, float hl, Color color)
    {
        var c0 = Vector3.Transform(new Vector3(-hw, -hh, -hl), matrix);
        var c1 = Vector3.Transform(new Vector3(hw, -hh, -hl), matrix);
        var c2 = Vector3.Transform(new Vector3(hw, hh, -hl), matrix);
        var c3 = Vector3.Transform(new Vector3(-hw, hh, -hl), matrix);
        var c4 = Vector3.Transform(new Vector3(-hw, -hh, hl), matrix);
        var c5 = Vector3.Transform(new Vector3(hw, -hh, hl), matrix);
        var c6 = Vector3.Transform(new Vector3(hw, hh, hl), matrix);
        var c7 = Vector3.Transform(new Vector3(-hw, hh, hl), matrix);

        DrawLine(debugDrawer, c0, c1, color);
        DrawLine(debugDrawer, c1, c2, color);
        DrawLine(debugDrawer, c2, c3, color);
        DrawLine(debugDrawer, c3, c0, color);

        DrawLine(debugDrawer, c4, c5, color);
        DrawLine(debugDrawer, c5, c6, color);
        DrawLine(debugDrawer, c6, c7, color);
        DrawLine(debugDrawer, c7, c4, color);

        DrawLine(debugDrawer, c0, c4, color);
        DrawLine(debugDrawer, c1, c5, color);
        DrawLine(debugDrawer, c2, c6, color);
        DrawLine(debugDrawer, c3, c7, color);
    }

    /// <summary>3 great circles (XY, XZ, YZ planes), 16 segments each.</summary>
    private static void DrawSphere(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, Color color)
    {
        for (int i = 0; i < CircleSegments; i++)
        {
            var a = UnitCircle[i];
            var b = UnitCircle[(i + 1) % CircleSegments];

            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(a.X * radius, a.Y * radius, 0f), matrix),
                Vector3.Transform(new Vector3(b.X * radius, b.Y * radius, 0f), matrix),
                color);

            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(a.X * radius, 0f, a.Y * radius), matrix),
                Vector3.Transform(new Vector3(b.X * radius, 0f, b.Y * radius), matrix),
                color);

            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(0f, a.X * radius, a.Y * radius), matrix),
                Vector3.Transform(new Vector3(0f, b.X * radius, b.Y * radius), matrix),
                color);
        }
    }

    /// <summary>Sphere-expanded segment along the local Y axis: a ring at each end plus 4 verticals for
    /// the cylindrical part, and a half-circle in each of the XY/YZ planes at each end for the domes.</summary>
    private static void DrawCapsule(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, float halfLength, Color color)
    {
        DrawRingXZ(debugDrawer, matrix, radius, halfLength, color);
        DrawRingXZ(debugDrawer, matrix, radius, -halfLength, color);
        DrawSideLines(debugDrawer, matrix, radius, halfLength, color);

        DrawHalfCircleXY(debugDrawer, matrix, radius, halfLength, 1f, color);
        DrawHalfCircleYZ(debugDrawer, matrix, radius, halfLength, 1f, color);
        DrawHalfCircleXY(debugDrawer, matrix, radius, -halfLength, -1f, color);
        DrawHalfCircleYZ(debugDrawer, matrix, radius, -halfLength, -1f, color);
    }

    /// <summary>Cylinder along the local Y axis: a ring at each end plus 4 verticals, no caps.</summary>
    private static void DrawCylinder(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, float halfLength, Color color)
    {
        DrawRingXZ(debugDrawer, matrix, radius, halfLength, color);
        DrawRingXZ(debugDrawer, matrix, radius, -halfLength, color);
        DrawSideLines(debugDrawer, matrix, radius, halfLength, color);
    }

    private static void DrawRingXZ(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, float y, Color color)
    {
        for (int i = 0; i < CircleSegments; i++)
        {
            var a = UnitCircle[i];
            var b = UnitCircle[(i + 1) % CircleSegments];

            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(a.X * radius, y, a.Y * radius), matrix),
                Vector3.Transform(new Vector3(b.X * radius, y, b.Y * radius), matrix),
                color);
        }
    }

    private static void DrawSideLines(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, float halfLength, Color color)
    {
        for (int i = 0; i < CircleSegments; i += CircleSegments / 4)
        {
            var c = UnitCircle[i];
            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(c.X * radius, halfLength, c.Y * radius), matrix),
                Vector3.Transform(new Vector3(c.X * radius, -halfLength, c.Y * radius), matrix),
                color);
        }
    }

    /// <summary>Half-circle (8 segments) in the local XY plane, centered at (0, centerY, 0), bulging
    /// toward +Y when <paramref name="sign"/> is 1 and toward -Y when -1 (the two hemisphere domes).</summary>
    private static void DrawHalfCircleXY(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, float centerY, float sign, Color color)
    {
        int half = CircleSegments / 2;
        for (int i = 0; i < half; i++)
        {
            var a = UnitCircle[i];
            var b = UnitCircle[i + 1];

            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(a.X * radius, centerY + sign * a.Y * radius, 0f), matrix),
                Vector3.Transform(new Vector3(b.X * radius, centerY + sign * b.Y * radius, 0f), matrix),
                color);
        }
    }

    /// <summary>Half-circle (8 segments) in the local YZ plane, same convention as <see cref="DrawHalfCircleXY"/>.</summary>
    private static void DrawHalfCircleYZ(IPhysicsDebugDrawer debugDrawer, Matrix matrix, float radius, float centerY, float sign, Color color)
    {
        int half = CircleSegments / 2;
        for (int i = 0; i < half; i++)
        {
            var a = UnitCircle[i];
            var b = UnitCircle[i + 1];

            DrawLine(debugDrawer,
                Vector3.Transform(new Vector3(0f, centerY + sign * a.Y * radius, a.X * radius), matrix),
                Vector3.Transform(new Vector3(0f, centerY + sign * b.Y * radius, b.X * radius), matrix),
                color);
        }
    }

    private static void DrawLine(IPhysicsDebugDrawer debugDrawer, Vector3 from, Vector3 to, Color color)
    {
        debugDrawer.DrawLine(ref from, ref to, color);
    }

    // ----- AABB: recomputed from shape + pose (not read back from the broad phase) -----------

    private static void DrawAabb(Simulation simulation, IPhysicsDebugDrawer debugDrawer, TypedIndex shapeIndex, RigidPose pose)
    {
        bool any = false;
        System.Numerics.Vector3 min = default;
        System.Numerics.Vector3 max = default;
        AccumulateBounds(simulation, shapeIndex, pose, ref min, ref max, ref any);

        if (!any)
        {
            return;
        }

        Vector3 xnaMin = min;
        Vector3 xnaMax = max;

        var c0 = new Vector3(xnaMin.X, xnaMin.Y, xnaMin.Z);
        var c1 = new Vector3(xnaMax.X, xnaMin.Y, xnaMin.Z);
        var c2 = new Vector3(xnaMax.X, xnaMax.Y, xnaMin.Z);
        var c3 = new Vector3(xnaMin.X, xnaMax.Y, xnaMin.Z);
        var c4 = new Vector3(xnaMin.X, xnaMin.Y, xnaMax.Z);
        var c5 = new Vector3(xnaMax.X, xnaMin.Y, xnaMax.Z);
        var c6 = new Vector3(xnaMax.X, xnaMax.Y, xnaMax.Z);
        var c7 = new Vector3(xnaMin.X, xnaMax.Y, xnaMax.Z);

        DrawLine(debugDrawer, c0, c1, AabbColor);
        DrawLine(debugDrawer, c1, c2, AabbColor);
        DrawLine(debugDrawer, c2, c3, AabbColor);
        DrawLine(debugDrawer, c3, c0, AabbColor);

        DrawLine(debugDrawer, c4, c5, AabbColor);
        DrawLine(debugDrawer, c5, c6, AabbColor);
        DrawLine(debugDrawer, c6, c7, AabbColor);
        DrawLine(debugDrawer, c7, c4, AabbColor);

        DrawLine(debugDrawer, c0, c4, AabbColor);
        DrawLine(debugDrawer, c1, c5, AabbColor);
        DrawLine(debugDrawer, c2, c6, AabbColor);
        DrawLine(debugDrawer, c3, c7, AabbColor);
    }

    private static void AccumulateBounds(Simulation simulation, TypedIndex shapeIndex, RigidPose pose, ref System.Numerics.Vector3 min, ref System.Numerics.Vector3 max, ref bool any)
    {
        int type = shapeIndex.Type;

        if (type == Compound.Id)
        {
            ref var compound = ref simulation.Shapes.GetShape<Compound>(shapeIndex.Index);
            for (int i = 0; i < compound.Children.Length; i++)
            {
                ref var child = ref compound.Children[i];
                RigidPose.MultiplyWithoutOverlap(new RigidPose(child.LocalPosition, child.LocalOrientation), pose, out var childPose);
                AccumulateBounds(simulation, child.ShapeIndex, childPose, ref min, ref max, ref any);
            }

            return;
        }

        System.Numerics.Vector3 localMin;
        System.Numerics.Vector3 localMax;

        if (type == BepuBox.Id)
        {
            ref var box = ref simulation.Shapes.GetShape<BepuBox>(shapeIndex.Index);
            box.ComputeBounds(pose.Orientation, out localMin, out localMax);
        }
        else if (type == BepuSphere.Id)
        {
            ref var sphere = ref simulation.Shapes.GetShape<BepuSphere>(shapeIndex.Index);
            sphere.ComputeBounds(pose.Orientation, out localMin, out localMax);
        }
        else if (type == BepuCapsule.Id)
        {
            ref var capsule = ref simulation.Shapes.GetShape<BepuCapsule>(shapeIndex.Index);
            capsule.ComputeBounds(pose.Orientation, out localMin, out localMax);
        }
        else if (type == BepuCylinder.Id)
        {
            ref var cylinder = ref simulation.Shapes.GetShape<BepuCylinder>(shapeIndex.Index);
            cylinder.ComputeBounds(pose.Orientation, out localMin, out localMax);
        }
        else
        {
            return;
        }

        var worldMin = localMin + pose.Position;
        var worldMax = localMax + pose.Position;

        if (!any)
        {
            min = worldMin;
            max = worldMax;
            any = true;
        }
        else
        {
            min = System.Numerics.Vector3.Min(min, worldMin);
            max = System.Numerics.Vector3.Max(max, worldMax);
        }
    }

    // ----- Contacts of the last step -----------------------------------------------------------

    private static void DrawContacts(BepuPhysicsEngine engine, IPhysicsDebugDrawer debugDrawer)
    {
        var records = engine.ContactBuffer.Records;
        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Depth < -ContactTolerance)
            {
                continue;
            }

            Vector3 point = engine.GetPosition(record.A) + record.Offset;
            Vector3 normal = record.Normal;
            debugDrawer.DrawContactPoint(ref point, ref normal, record.Depth, 0, ContactColor);
        }
    }
}
