using System;
using System.Collections.Generic;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime.Overlays;

public sealed class EditorLightWireOverlayRenderer : IDisposable
{
    private const int CircleSegments = 48;
    private const int MaxLineVertices = 4096;
    private const float DirectionalArrowLength = 2.75f;
    private const float DirectionalArrowSpacing = 0.5f;
    private const float DirectionalArrowHeadLength = 0.35f;
    private const float DirectionalArrowHeadWidth = 0.18f;

    private readonly Vector2[] _circlePoints = new Vector2[CircleSegments];
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[MaxLineVertices];
    private readonly Effect _effect;
    private int _vertexCount;

    public EditorLightWireOverlayRenderer(ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _effect = content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = MathHelper.TwoPi * index / CircleSegments;
            _circlePoints[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
    }

    public void Draw(GraphicsDevice graphicsDevice, in RenderFrame frame, IReadOnlyList<EditorLightOverlayItem> lights)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(lights);

        _vertexCount = 0;

        for (int index = 0; index < lights.Count; index++)
        {
            var light = lights[index];
            if (!light.IsSelected)
            {
                continue;
            }

            AddLightHelper(in light);
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

    private void AddLightHelper(in EditorLightOverlayItem light)
    {
        Color color = GetOverlayColor(light.Color);

        switch (light.Type)
        {
            case LightType.Point:
                AddPointLightHelper(in light, color);
                break;

            case LightType.Spot:
                AddSpotLightHelper(in light, color);
                break;

            case LightType.Directional:
                AddDirectionalLightHelper(in light, color);
                break;
        }
    }

    private void AddPointLightHelper(in EditorLightOverlayItem light, Color color)
    {
        if (light.Range <= 0.0f)
        {
            return;
        }

        AddCircle(light.Position, Vector3.Right, Vector3.Up, light.Range, color);
        AddCircle(light.Position, Vector3.Right, Vector3.Forward, light.Range, color);
        AddCircle(light.Position, Vector3.Up, Vector3.Forward, light.Range, color);
    }

    private void AddSpotLightHelper(in EditorLightOverlayItem light, Color color)
    {
        if (light.Range <= 0.0f || light.OuterConeAngleRadians <= 0.0f)
        {
            return;
        }

        Vector3 direction = Normalize(light.Direction, Vector3.Forward);
        BuildBasis(direction, out Vector3 right, out Vector3 up);

        float clampedAngle = MathHelper.Clamp(light.OuterConeAngleRadians, 0.0f, MathHelper.PiOver2 - 0.001f);
        float baseRadius = light.Range * MathF.Tan(clampedAngle);
        Vector3 baseCenter = light.Position + direction * light.Range;

        AddLine(light.Position, baseCenter, color);
        AddCircle(baseCenter, right, up, baseRadius, color);

        int sideStep = CircleSegments / 4;
        for (int index = 0; index < CircleSegments; index += sideStep)
        {
            Vector2 point = _circlePoints[index];
            Vector3 edge = baseCenter + (right * point.X + up * point.Y) * baseRadius;
            AddLine(light.Position, edge, color);
        }
    }

    private void AddDirectionalLightHelper(in EditorLightOverlayItem light, Color color)
    {
        Vector3 direction = Normalize(light.Direction, Vector3.Forward);
        BuildBasis(direction, out Vector3 right, out Vector3 up);

        AddDirectionalArrow(light.Position - right * DirectionalArrowSpacing, direction, right, up, color);
        AddDirectionalArrow(light.Position, direction, right, up, color);
        AddDirectionalArrow(light.Position + right * DirectionalArrowSpacing, direction, right, up, color);
    }

    private void AddDirectionalArrow(Vector3 center, Vector3 direction, Vector3 right, Vector3 up, Color color)
    {
        Vector3 start = center - direction * (DirectionalArrowLength * 0.5f);
        Vector3 end = center + direction * (DirectionalArrowLength * 0.5f);
        Vector3 headBase = end - direction * DirectionalArrowHeadLength;

        AddLine(start, end, color);
        AddLine(end, headBase + right * DirectionalArrowHeadWidth, color);
        AddLine(end, headBase - right * DirectionalArrowHeadWidth, color);
        AddLine(end, headBase + up * DirectionalArrowHeadWidth, color);
        AddLine(end, headBase - up * DirectionalArrowHeadWidth, color);
    }

    private void AddCircle(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, Color color)
    {
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
            AddLine(start, end, color);
        }
    }

    private void AddLine(Vector3 start, Vector3 end, Color color)
    {
        if (_vertexCount + 2 > _vertices.Length)
        {
            return;
        }

        _vertices[_vertexCount++] = new VertexPositionColor(start, color);
        _vertices[_vertexCount++] = new VertexPositionColor(end, color);
    }

    private static void BuildBasis(Vector3 direction, out Vector3 right, out Vector3 up)
    {
        Vector3 helper = MathF.Abs(Vector3.Dot(direction, Vector3.Up)) > 0.95f
            ? Vector3.Right
            : Vector3.Up;

        right = Vector3.Cross(helper, direction);
        right = Normalize(right, Vector3.Right);
        up = Vector3.Cross(direction, right);
        up = Normalize(up, Vector3.Up);
    }

    private static Vector3 Normalize(Vector3 value, Vector3 fallback)
    {
        if (!float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || !float.IsFinite(value.Z)
            || value.LengthSquared() < 0.000001f)
        {
            return fallback;
        }

        value.Normalize();
        return value;
    }

    private static Color GetOverlayColor(Color source)
    {
        const byte minimum = 96;
        return new Color(
            Math.Max(source.R, minimum),
            Math.Max(source.G, minimum),
            Math.Max(source.B, minimum),
            (byte)230);
    }
}