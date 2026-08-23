using System;
﻿using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application.Components.Physics;

public class PhysicsDebugDrawComponent : IPhysicsDebugDrawer
{
    private readonly Line3dRendererComponent _line3dRendererComponent;

    public PhysicsDebugDrawModes DebugMode { get; set; }

    public PhysicsDebugDrawComponent(Line3dRendererComponent line3dRendererComponent)
    {
        _line3dRendererComponent = line3dRendererComponent;
    }

    public void Draw3dText(ref Vector3 location, string textString)
    {
        throw new NotImplementedException();
    }

    /// <summary>Length of the tick drawn along a contact normal when the penetration is smaller than it.</summary>
    private const float MinContactTickLength = 0.1f;

    public void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color)
    {
        //A short tick along the normal, sized by the penetration: a unit-long normal would dwarf small bodies.
        float length = MathF.Max(MinContactTickLength, MathF.Abs(distance));
        _line3dRendererComponent.AddLine(pointOnB, pointOnB + normalOnB * length, color);
    }

    public void DrawLine(ref Vector3 from, ref Vector3 to, Color color)
    {
        _line3dRendererComponent.AddLine(from, to, color);
    }

    public void DrawDebugWorld(IPhysicsWorld physicsWorld)
    {
        if (physicsWorld == null)
        {
            return;
        }

        physicsWorld.DrawDebugWorld(this);
    }

    public void ReportErrorWarning(string warningString)
    {
        throw new NotImplementedException();
    }
}