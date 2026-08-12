using System;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Engine.Geometry;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace CasaEngine.Editor.Runtime.Overlays;

internal sealed class EditorSpriteOverlayRenderer : IDisposable
{
    private const int CircleSegments = 48;

    private static readonly Color HotspotColor = new(255, 205, 64, 240);

    private readonly Vector2[] _circlePoints = new Vector2[CircleSegments];

    public EditorSpriteOverlayRenderer()
    {
        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = MathHelper.TwoPi * index / CircleSegments;
            _circlePoints[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
    }

    public void Draw(
        IUIDrawContext drawContext,
        Rectangle viewportBounds,
        SpriteData spriteData,
        Vector3 spritePosition,
        Vector2 spriteScale,
        float pixelsPerUnit,
        bool showCollisions,
        bool showHotspot,
        float opacity = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(drawContext);

        if (spriteData == null
            || viewportBounds.Width <= 0
            || viewportBounds.Height <= 0
            || pixelsPerUnit <= 0f
            || (!showCollisions && !showHotspot))
        {
            return;
        }

        var mapper = ScreenSpaceMapper.Create(viewportBounds, spriteData, spritePosition, spriteScale, pixelsPerUnit);

        if (showHotspot)
        {
            float hotspotHalfSize = Math.Clamp(MathF.Min(spriteData.PositionInTexture.Width, spriteData.PositionInTexture.Height) * 0.08f, 4f, 12f);
            DrawHotspot(drawContext, in mapper, spritePosition, hotspotHalfSize, HotspotColor * opacity);
        }

        if (!showCollisions)
        {
            return;
        }

        for (int collisionIndex = 0; collisionIndex < spriteData.CollisionShapes.Count; collisionIndex++)
        {
            Collision2d collision = spriteData.CollisionShapes[collisionIndex];
            Color collisionColor = collision.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
            Color strokeColor = collisionColor * opacity;
            switch (collision.Shape)
            {
                case ShapeRectangle rectangle:
                    DrawRectangle(drawContext, in mapper, spritePosition, spriteScale, spriteData.Origin, rectangle, strokeColor);
                    break;

                case ShapeCircle circle:
                    DrawCircle(drawContext, in mapper, spritePosition, spriteScale, spriteData.Origin, circle, strokeColor);
                    break;
            }
        }
    }

    public void Dispose()
    {
    }

    private void DrawHotspot(IUIDrawContext drawContext, in ScreenSpaceMapper mapper, Vector3 spritePosition, float hotspotHalfSize, Color color)
    {
        Vector2 center = mapper.WorldToScreen(new Vector2(spritePosition.X, spritePosition.Y));
        float horizontalHalfLength = hotspotHalfSize * mapper.PixelWidth;
        float verticalHalfLength = hotspotHalfSize * mapper.PixelHeight;

        drawContext.StrokeLineSegment(
            Vector2.Zero,
            new Vector2(center.X - horizontalHalfLength, center.Y),
            new Vector2(center.X + horizontalHalfLength, center.Y),
            color,
            mapper.PixelHeight);
        drawContext.StrokeLineSegment(
            Vector2.Zero,
            new Vector2(center.X, center.Y - verticalHalfLength),
            new Vector2(center.X, center.Y + verticalHalfLength),
            color,
            mapper.PixelWidth);
    }

    private void DrawRectangle(
        IUIDrawContext drawContext,
        in ScreenSpaceMapper mapper,
        Vector3 spritePosition,
        Vector2 spriteScale,
        Point origin,
        ShapeRectangle rectangle,
        Color color)
    {
        float x = spritePosition.X + (rectangle.Position.X - origin.X) * spriteScale.X;
        float y = spritePosition.Y - (rectangle.Position.Y - origin.Y + rectangle.Height) * spriteScale.Y;
        float width = rectangle.Width * spriteScale.X;
        float height = rectangle.Height * spriteScale.Y;

        Vector2 point0 = mapper.WorldToScreen(new Vector2(x, y));
        Vector2 point1 = mapper.WorldToScreen(new Vector2(x + width, y));
        Vector2 point2 = mapper.WorldToScreen(new Vector2(x + width, y + height));
        Vector2 point3 = mapper.WorldToScreen(new Vector2(x, y + height));

        float left = MathF.Min(MathF.Min(point0.X, point1.X), MathF.Min(point2.X, point3.X));
        float right = MathF.Max(MathF.Max(point0.X, point1.X), MathF.Max(point2.X, point3.X));
        float top = MathF.Min(MathF.Min(point0.Y, point1.Y), MathF.Min(point2.Y, point3.Y));
        float bottom = MathF.Max(MathF.Max(point0.Y, point1.Y), MathF.Max(point2.Y, point3.Y));

        DrawRectangleOutline(drawContext, new RectangleF(left, top, right - left, bottom - top), color, mapper.PixelWidth, mapper.PixelHeight);
    }

    private void DrawCircle(
        IUIDrawContext drawContext,
        in ScreenSpaceMapper mapper,
        Vector3 spritePosition,
        Vector2 spriteScale,
        Point origin,
        ShapeCircle circle,
        Color color)
    {
        float radiusX = circle.Radius * mapper.PixelWidth;
        float radiusY = circle.Radius * mapper.PixelHeight;
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return;
        }

        Vector2 center = mapper.WorldToScreen(new Vector2(
            spritePosition.X + (circle.Position.X - origin.X) * spriteScale.X,
            spritePosition.Y - (circle.Position.Y - origin.Y) * spriteScale.Y));
        float strokeThickness = Math.Max(1f, MathF.Max(mapper.PixelWidth, mapper.PixelHeight));

        for (int index = 0; index < CircleSegments; index++)
        {
            int nextIndex = index + 1;
            if (nextIndex == CircleSegments)
            {
                nextIndex = 0;
            }

            Vector2 point = _circlePoints[index];
            Vector2 nextPoint = _circlePoints[nextIndex];
            Vector2 start = new(center.X + point.X * radiusX, center.Y - point.Y * radiusY);
            Vector2 end = new(center.X + nextPoint.X * radiusX, center.Y - nextPoint.Y * radiusY);
            drawContext.StrokeLineSegment(Vector2.Zero, start, end, color, strokeThickness);
        }
    }

    private static void DrawRectangleOutline(IUIDrawContext drawContext, RectangleF rectangle, Color color, float verticalStrokeThickness, float horizontalStrokeThickness)
    {
        if (rectangle.Width <= 0f || rectangle.Height <= 0f)
        {
            return;
        }

        float topThickness = Math.Min(horizontalStrokeThickness, rectangle.Height);
        float bottomThickness = topThickness;
        float leftThickness = Math.Min(verticalStrokeThickness, rectangle.Width);
        float rightThickness = leftThickness;

        drawContext.FillRectangle(Vector2.Zero, new RectangleF(rectangle.Left, rectangle.Top, rectangle.Width, topThickness), color);
        drawContext.FillRectangle(Vector2.Zero, new RectangleF(rectangle.Left, rectangle.Bottom - bottomThickness, rectangle.Width, bottomThickness), color);
        drawContext.FillRectangle(Vector2.Zero, new RectangleF(rectangle.Left, rectangle.Top, leftThickness, rectangle.Height), color);
        drawContext.FillRectangle(Vector2.Zero, new RectangleF(rectangle.Right - rightThickness, rectangle.Top, rightThickness, rectangle.Height), color);
    }

    private readonly record struct ScreenSpaceMapper(Vector2 ViewportCenter, Vector2 FocusWorld, float PixelsPerUnit, float PixelWidth, float PixelHeight)
    {
        public static ScreenSpaceMapper Create(Rectangle viewportBounds, SpriteData spriteData, Vector3 spritePosition, Vector2 spriteScale, float pixelsPerUnit)
        {
            BoundingBox localBounds = SpriteDataBoundsCalculator.CalculateLocalBounds(spriteData);
            float focusX = spritePosition.X + ((localBounds.Min.X + localBounds.Max.X) * 0.5f * spriteScale.X);
            float focusY = spritePosition.Y + ((localBounds.Min.Y + localBounds.Max.Y) * 0.5f * spriteScale.Y);

            return new ScreenSpaceMapper(
                new Vector2(viewportBounds.Left + viewportBounds.Width * 0.5f, viewportBounds.Top + viewportBounds.Height * 0.5f),
                new Vector2(focusX, focusY),
                pixelsPerUnit,
                Math.Max(1f, MathF.Abs(spriteScale.X) * pixelsPerUnit),
                Math.Max(1f, MathF.Abs(spriteScale.Y) * pixelsPerUnit));
        }

        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return new Vector2(
                ViewportCenter.X + (worldPosition.X - FocusWorld.X) * PixelsPerUnit,
                ViewportCenter.Y - (worldPosition.Y - FocusWorld.Y) * PixelsPerUnit);
        }
    }
}