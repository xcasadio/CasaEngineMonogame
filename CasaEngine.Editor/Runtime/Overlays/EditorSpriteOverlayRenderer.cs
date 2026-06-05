using System;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Geometry;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime.Overlays;

internal sealed class EditorSpriteOverlayRenderer : IDisposable
{
    private const int CircleSegments = 48;
    private const int MaxLineVertices = 2048;

    private static readonly Color HotspotColor = new(255, 205, 64, 240);

    private readonly Vector2[] _circlePoints = new Vector2[CircleSegments];
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[MaxLineVertices];
    private readonly Effect _effect;
    private int _vertexCount;

    public EditorSpriteOverlayRenderer(ContentManager content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _effect = content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = MathHelper.TwoPi * index / CircleSegments;
            _circlePoints[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
    }

    public void Draw(
        GraphicsDevice graphicsDevice,
        in RenderFrame frame,
        SpriteData? spriteData,
        Vector3 spritePosition,
        Vector2 spriteScale,
        bool showCollisions,
        bool showHotspot)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (spriteData == null || (!showCollisions && !showHotspot))
        {
            return;
        }

        _vertexCount = 0;

        if (showHotspot)
        {
            float hotspotHalfSize = Math.Clamp(MathF.Min(spriteData.PositionInTexture.Width, spriteData.PositionInTexture.Height) * 0.08f, 4f, 12f);
            AddLine(
                new Vector3(spritePosition.X - hotspotHalfSize, spritePosition.Y, spritePosition.Z - 0.001f),
                new Vector3(spritePosition.X + hotspotHalfSize, spritePosition.Y, spritePosition.Z - 0.001f),
                HotspotColor);
            AddLine(
                new Vector3(spritePosition.X, spritePosition.Y - hotspotHalfSize, spritePosition.Z - 0.001f),
                new Vector3(spritePosition.X, spritePosition.Y + hotspotHalfSize, spritePosition.Z - 0.001f),
                HotspotColor);
        }

        if (showCollisions)
        {
            for (int collisionIndex = 0; collisionIndex < spriteData.CollisionShapes.Count; collisionIndex++)
            {
                Collision2d collision = spriteData.CollisionShapes[collisionIndex];
                Color collisionColor = collision.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
                switch (collision.Shape)
                {
                    case ShapeRectangle rectangle:
                        AddRectangle(spritePosition, spriteScale, spriteData.Origin, rectangle, collisionColor);
                        break;

                    case ShapeCircle circle:
                        AddCircle(spritePosition, spriteScale, spriteData.Origin, circle, collisionColor);
                        break;
                }
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
        for (int passIndex = 0; passIndex < _effect.CurrentTechnique.Passes.Count; passIndex++)
        {
            _effect.CurrentTechnique.Passes[passIndex].Apply();
            graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _vertices, 0, primitiveCount);
        }
    }

    public void Dispose()
    {
        _effect.Dispose();
    }

    private void AddRectangle(Vector3 spritePosition, Vector2 spriteScale, Point origin, ShapeRectangle rectangle, Color color)
    {
        float x = spritePosition.X + (rectangle.Position.X - origin.X) * spriteScale.X;
        float y = spritePosition.Y - (rectangle.Position.Y - origin.Y + rectangle.Height) * spriteScale.Y;
        float width = rectangle.Width * spriteScale.X;
        float height = rectangle.Height * spriteScale.Y;
        float z = spritePosition.Z - 0.001f;

        Vector3 topLeft = new(x, y, z);
        Vector3 topRight = new(x + width, y, z);
        Vector3 bottomLeft = new(x, y + height, z);
        Vector3 bottomRight = new(x + width, y + height, z);

        AddLine(topLeft, topRight, color);
        AddLine(topLeft, bottomLeft, color);
        AddLine(topRight, bottomRight, color);
        AddLine(bottomLeft, bottomRight, color);
    }

    private void AddCircle(Vector3 spritePosition, Vector2 spriteScale, Point origin, ShapeCircle circle, Color color)
    {
        float radius = circle.Radius * MathF.Max(MathF.Abs(spriteScale.X), MathF.Abs(spriteScale.Y));
        if (radius <= 0f)
        {
            return;
        }

        Vector3 center = new(
            spritePosition.X + (circle.Position.X - origin.X) * spriteScale.X,
            spritePosition.Y - (circle.Position.Y - origin.Y) * spriteScale.Y,
            spritePosition.Z - 0.001f);

        for (int index = 0; index < CircleSegments; index++)
        {
            int nextIndex = index + 1;
            if (nextIndex == CircleSegments)
            {
                nextIndex = 0;
            }

            Vector2 point = _circlePoints[index];
            Vector2 nextPoint = _circlePoints[nextIndex];
            Vector3 start = center + new Vector3(point.X * radius, point.Y * radius, 0f);
            Vector3 end = center + new Vector3(nextPoint.X * radius, nextPoint.Y * radius, 0f);
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
}