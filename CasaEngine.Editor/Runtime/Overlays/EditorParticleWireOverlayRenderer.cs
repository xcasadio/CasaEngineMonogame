using System;
using System.Collections.Generic;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime.Overlays;

public sealed class EditorParticleWireOverlayRenderer : IDisposable
{
    private const int CircleSegments = 48;
    private const int MaxLineVertices = 8192;
    private const float PointGizmoSize = 0.25f;
    private const float ConeLength = 1.5f;

    private static readonly Color ShapeColor = new(91, 211, 255, 230);
    private static readonly Color BoundsColor = new(255, 210, 91, 190);

    private readonly Vector2[] _circlePoints = new Vector2[CircleSegments];
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[MaxLineVertices];
    private readonly Effect _effect;
    private int _vertexCount;

    public EditorParticleWireOverlayRenderer(ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _effect = content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = MathHelper.TwoPi * index / CircleSegments;
            _circlePoints[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
    }

    public int LastDrawnItemCount { get; private set; }

    public int LastDrawnLineCount { get; private set; }

    public void Draw(GraphicsDevice graphicsDevice, in RenderFrame frame, IReadOnlyList<EditorParticleOverlayItem> particles)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(particles);

        _vertexCount = 0;
        LastDrawnItemCount = 0;
        LastDrawnLineCount = 0;

        for (int index = 0; index < particles.Count; index++)
        {
            var particle = particles[index];
            if (!particle.IsSelected)
            {
                continue;
            }

            int beforeVertexCount = _vertexCount;
            AddParticleHelper(in particle);
            if (_vertexCount > beforeVertexCount)
            {
                LastDrawnItemCount++;
            }
        }

        if (_vertexCount == 0)
        {
            return;
        }

        using var guard = new GraphicsStateGuard(graphicsDevice);

        graphicsDevice.DepthStencilState = DepthStencilState.None;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        graphicsDevice.BlendState = BlendState.AlphaBlend;
        graphicsDevice.Indices = null;

        _effect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(frame.View * frame.Projection);
        _effect.Parameters[ShaderParameterNames.ColorMultiplier]?.SetValue(Vector4.One);

        int primitiveCount = _vertexCount / 2;
        LastDrawnLineCount = primitiveCount;
        for (int index = 0; index < _effect.CurrentTechnique.Passes.Count; index++)
        {
            _effect.CurrentTechnique.Passes[index].Apply();
            graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _vertices, 0, primitiveCount);
        }
    }

    public void Dispose()
    {
        _effect.Dispose();
    }

    private void AddParticleHelper(in EditorParticleOverlayItem particle)
    {
        var emitters = particle.Asset.Emitters;
        for (int emitterIndex = 0; emitterIndex < emitters.Count; emitterIndex++)
        {
            ParticleEmitterDefinition emitter = emitters[emitterIndex];
            if (!emitter.Enabled)
            {
                continue;
            }

            AddEmitterShape(emitter.Shape, particle.WorldMatrix, ShapeColor);
        }

        if (particle.HasBounds)
        {
            AddBoundingBox(particle.Bounds, BoundsColor);
        }
    }

    private void AddEmitterShape(ParticleShapeModule shape, Matrix worldMatrix, Color color)
    {
        switch (shape.ShapeType)
        {
            case ParticleShapeType.Circle:
                AddCircle(worldMatrix, Vector3.Zero, Vector3.Right, Vector3.Up, MathF.Max(0.0f, shape.Radius), color);
                break;

            case ParticleShapeType.Box:
                AddBox(worldMatrix, shape.Size, color);
                break;

            case ParticleShapeType.Sphere:
                AddSphere(worldMatrix, MathF.Max(0.0f, shape.Radius), color);
                break;

            case ParticleShapeType.Cone:
                AddCone(worldMatrix, shape, color);
                break;

            case ParticleShapeType.Point:
            default:
                AddPoint(worldMatrix, color);
                break;
        }
    }

    private void AddPoint(Matrix worldMatrix, Color color)
    {
        AddLocalLine(worldMatrix, new Vector3(-PointGizmoSize, 0.0f, 0.0f), new Vector3(PointGizmoSize, 0.0f, 0.0f), color);
        AddLocalLine(worldMatrix, new Vector3(0.0f, -PointGizmoSize, 0.0f), new Vector3(0.0f, PointGizmoSize, 0.0f), color);
        AddLocalLine(worldMatrix, new Vector3(0.0f, 0.0f, -PointGizmoSize), new Vector3(0.0f, 0.0f, PointGizmoSize), color);
    }

    private void AddSphere(Matrix worldMatrix, float radius, Color color)
    {
        AddCircle(worldMatrix, Vector3.Zero, Vector3.Right, Vector3.Up, radius, color);
        AddCircle(worldMatrix, Vector3.Zero, Vector3.Right, Vector3.Forward, radius, color);
        AddCircle(worldMatrix, Vector3.Zero, Vector3.Up, Vector3.Forward, radius, color);
    }

    private void AddCone(Matrix worldMatrix, ParticleShapeModule shape, Color color)
    {
        float length = MathF.Max(ConeLength, shape.Radius);
        float angleRadians = MathHelper.ToRadians(MathHelper.Clamp(shape.AngleDegrees, 0.0f, 80.0f));
        float baseRadius = length * MathF.Tan(angleRadians);
        Vector3 baseCenter = Vector3.Up * length;

        AddCircle(worldMatrix, baseCenter, Vector3.Right, Vector3.Forward, baseRadius, color);
        AddLocalLine(worldMatrix, Vector3.Zero, baseCenter, color);

        int sideStep = CircleSegments / 4;
        for (int index = 0; index < CircleSegments; index += sideStep)
        {
            Vector2 point = _circlePoints[index];
            Vector3 edge = baseCenter + new Vector3(point.X * baseRadius, 0.0f, point.Y * baseRadius);
            AddLocalLine(worldMatrix, Vector3.Zero, edge, color);
        }
    }

    private void AddBox(Matrix worldMatrix, Vector3 size, Color color)
    {
        Vector3 halfSize = new(
            MathF.Max(0.0f, size.X) * 0.5f,
            MathF.Max(0.0f, size.Y) * 0.5f,
            MathF.Max(0.0f, size.Z) * 0.5f);

        Vector3 min = -halfSize;
        Vector3 max = halfSize;
        AddLocalBox(worldMatrix, min, max, color);
    }

    private void AddBoundingBox(BoundingBox bounds, Color color)
    {
        Vector3 min = bounds.Min;
        Vector3 max = bounds.Max;
        AddWorldBox(min, max, color);
    }

    private void AddCircle(Matrix worldMatrix, Vector3 center, Vector3 axisA, Vector3 axisB, float radius, Color color)
    {
        if (radius <= 0.0f)
        {
            AddPoint(worldMatrix, color);
            return;
        }

        for (int index = 0; index < CircleSegments; index++)
        {
            int nextIndex = index + 1;
            if (nextIndex == CircleSegments)
            {
                nextIndex = 0;
            }

            Vector2 point = _circlePoints[index];
            Vector2 nextPoint = _circlePoints[nextIndex];
            Vector3 start = center + (axisA * point.X + axisB * point.Y) * radius;
            Vector3 end = center + (axisA * nextPoint.X + axisB * nextPoint.Y) * radius;
            AddLocalLine(worldMatrix, start, end, color);
        }
    }

    private void AddLocalBox(Matrix worldMatrix, Vector3 min, Vector3 max, Color color)
    {
        Vector3 nearLeftBottom = new(min.X, min.Y, min.Z);
        Vector3 nearRightBottom = new(max.X, min.Y, min.Z);
        Vector3 nearRightTop = new(max.X, max.Y, min.Z);
        Vector3 nearLeftTop = new(min.X, max.Y, min.Z);
        Vector3 farLeftBottom = new(min.X, min.Y, max.Z);
        Vector3 farRightBottom = new(max.X, min.Y, max.Z);
        Vector3 farRightTop = new(max.X, max.Y, max.Z);
        Vector3 farLeftTop = new(min.X, max.Y, max.Z);

        AddLocalLine(worldMatrix, nearLeftBottom, nearRightBottom, color);
        AddLocalLine(worldMatrix, nearRightBottom, nearRightTop, color);
        AddLocalLine(worldMatrix, nearRightTop, nearLeftTop, color);
        AddLocalLine(worldMatrix, nearLeftTop, nearLeftBottom, color);
        AddLocalLine(worldMatrix, farLeftBottom, farRightBottom, color);
        AddLocalLine(worldMatrix, farRightBottom, farRightTop, color);
        AddLocalLine(worldMatrix, farRightTop, farLeftTop, color);
        AddLocalLine(worldMatrix, farLeftTop, farLeftBottom, color);
        AddLocalLine(worldMatrix, nearLeftBottom, farLeftBottom, color);
        AddLocalLine(worldMatrix, nearRightBottom, farRightBottom, color);
        AddLocalLine(worldMatrix, nearRightTop, farRightTop, color);
        AddLocalLine(worldMatrix, nearLeftTop, farLeftTop, color);
    }

    private void AddWorldBox(Vector3 min, Vector3 max, Color color)
    {
        Vector3 nearLeftBottom = new(min.X, min.Y, min.Z);
        Vector3 nearRightBottom = new(max.X, min.Y, min.Z);
        Vector3 nearRightTop = new(max.X, max.Y, min.Z);
        Vector3 nearLeftTop = new(min.X, max.Y, min.Z);
        Vector3 farLeftBottom = new(min.X, min.Y, max.Z);
        Vector3 farRightBottom = new(max.X, min.Y, max.Z);
        Vector3 farRightTop = new(max.X, max.Y, max.Z);
        Vector3 farLeftTop = new(min.X, max.Y, max.Z);

        AddLine(nearLeftBottom, nearRightBottom, color);
        AddLine(nearRightBottom, nearRightTop, color);
        AddLine(nearRightTop, nearLeftTop, color);
        AddLine(nearLeftTop, nearLeftBottom, color);
        AddLine(farLeftBottom, farRightBottom, color);
        AddLine(farRightBottom, farRightTop, color);
        AddLine(farRightTop, farLeftTop, color);
        AddLine(farLeftTop, farLeftBottom, color);
        AddLine(nearLeftBottom, farLeftBottom, color);
        AddLine(nearRightBottom, farRightBottom, color);
        AddLine(nearRightTop, farRightTop, color);
        AddLine(nearLeftTop, farLeftTop, color);
    }

    private void AddLocalLine(Matrix worldMatrix, Vector3 start, Vector3 end, Color color)
        => AddLine(Vector3.Transform(start, worldMatrix), Vector3.Transform(end, worldMatrix), color);

    private void AddLine(Vector3 start, Vector3 end, Color color)
    {
        if (_vertexCount + 2 > _vertices.Length)
        {
            return;
        }

        _vertices[_vertexCount++] = new VertexPositionColor(start, color);
        _vertices[_vertexCount++] = new VertexPositionColor(end, color);
    }
}