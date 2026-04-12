using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Triangulation;
using MonoGame.Extended.VectorDraw;
using WindingOrder = MonoGame.Extended.Triangulation.WindingOrder;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Primitives;

internal sealed class CasaLegacyShapeRenderer2D : IShapeRenderer2D
{
    private readonly CasaDrawTransaction _owner;

    private const int CircleMaxSides = 256;

    public CasaLegacyShapeRenderer2D(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public void StrokeAndFillRectangle(Vector2 origin, RectangleF destination, Color strokeColor, Color fillColor, Thickness strokeThickness, DrawContext? preferredContext = null)
    {
        FillRectangle(origin, destination.GetCompressed(strokeThickness), fillColor, preferredContext);
        StrokeRectangle(origin, destination, strokeColor, strokeThickness, preferredContext);
    }

    public void StrokeRectangle(Vector2 origin, RectangleF destination, Color color, Thickness thickness, DrawContext? preferredContext = null)
    {
        if (destination.Width.IsAlmostZero() || destination.Height.IsAlmostZero() || thickness.IsEmpty())
        {
            return;
        }

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        float leftEdgeWidth = Math.Min(thickness.Left, destination.Width);
        float topEdgeHeight = Math.Min(thickness.Top, destination.Height);
        float rightEdgeWidth = Math.Min(thickness.Right, destination.Width);
        float bottomEdgeHeight = Math.Min(thickness.Bottom, destination.Height);

        if (leftEdgeWidth > 0.0f && !leftEdgeWidth.IsAlmostZero())
        {
            RectangleF leftEdge = new(destination.Left, destination.Top + topEdgeHeight, leftEdgeWidth, destination.Height - topEdgeHeight - bottomEdgeHeight);
            FillRectangle(origin, leftEdge, color, context);
        }

        if (topEdgeHeight > 0.0f && !topEdgeHeight.IsAlmostZero())
        {
            RectangleF topEdge = new(destination.Left, destination.Top, destination.Width, topEdgeHeight);
            FillRectangle(origin, topEdge, color, context);
        }

        if (rightEdgeWidth > 0.0f && !rightEdgeWidth.IsAlmostZero())
        {
            RectangleF rightEdge = new(destination.Right - rightEdgeWidth, destination.Top + topEdgeHeight, rightEdgeWidth, destination.Height - topEdgeHeight - bottomEdgeHeight);
            FillRectangle(origin, rightEdge, color, context);
        }

        if (bottomEdgeHeight > 0.0f && !bottomEdgeHeight.IsAlmostZero())
        {
            RectangleF bottomEdge = new(destination.Left, destination.Bottom - bottomEdgeHeight, destination.Width, bottomEdgeHeight);
            FillRectangle(origin, bottomEdge, color, context);
        }
    }

    public void FillRectangle(Vector2 origin, RectangleF destination, Color color, DrawContext? preferredContext = null)
    {
        if (destination.Width.IsAlmostZero() || destination.Height.IsAlmostZero() || color.A == 0)
        {
            return;
        }

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        switch (context)
        {
            case DrawContext.Sprites:
                _owner.SpriteBatch.FillRectangle(destination.GetTranslated(origin), color);
                break;

            case DrawContext.Primitives:
                Vector2[] rectangleVertices =
                [
                    new Vector2(0, 0),
                    new Vector2(destination.Width, 0),
                    new Vector2(destination.Width, destination.Height),
                    new Vector2(0, destination.Height)
                ];
                _owner.PrimitiveDrawing.DrawSolidPolygon(destination.Position + origin, rectangleVertices, color, false);
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {context}");
        }
    }

    public void StrokeAndFillCircle(Vector2 center, Color strokeColor, Color fillColor, float radius, float strokeThickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
    {
        if (radius <= 0.0f || radius.IsAlmostZero())
        {
            return;
        }

        if (radius.IsAlmostEqual(strokeThickness))
        {
            FillCircle(center, strokeColor, radius, numSides, preferredContext);
            return;
        }

        FillCircle(center, fillColor, radius, numSides, preferredContext);
        StrokeCircle(center, strokeColor, radius, strokeThickness, numSides, preferredContext);
    }

    public void StrokeCircle(Vector2 center, Color color, float radius, float thickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
    {
        if (radius <= 0.0f || radius.IsAlmostZero() || thickness <= 0.0f || thickness.IsAlmostZero())
        {
            return;
        }

        ValidateCircleSides(nameof(StrokeCircle), numSides);

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        switch (context)
        {
            case DrawContext.Sprites:
                _owner.SpriteBatch.DrawCircle(center, radius, numSides, color, thickness, 0.0f);
                break;

            case DrawContext.Primitives:
                List<Vector2> vertices = CasaShapeGeometry.GetCircleVertices(center, radius, numSides)
                    .Select(vertex => vertex + (center - vertex).NormalizedCopy().Scale(thickness / 2f))
                    .ToList();

                List<(Vector2 Start, Vector2 End)> edges = vertices.SelectConsecutivePairs(true).ToList();
                for (int i = 0; i < edges.Count; i++)
                {
                    (Vector2 Start, Vector2 End) currentEdge = edges[i];
                    (Vector2 Start, Vector2 End) previousEdge = edges[(i - 1 + edges.Count) % edges.Count];

                    StrokeLineSegment(Vector2.Zero, currentEdge.Start, currentEdge.End, color, thickness, context);

                    Vector2 v0 = currentEdge.Start;
                    Vector2 v1 = v0 + (currentEdge.End - currentEdge.Start).RightNormal().NormalizedCopy().Scale(thickness / 2f);
                    Vector2 v2 = v0 + (previousEdge.Start - currentEdge.Start).LeftNormal().NormalizedCopy().Scale(thickness / 2f);
                    FillTrianglePrimitive(Vector2.Zero, v0, v1, v2, color);
                }
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {context}");
        }
    }

    public void FillCircle(Vector2 center, Color color, float radius, int numSides = 32, DrawContext? preferredContext = null)
    {
        if (radius <= 0.0f || radius.IsAlmostZero())
        {
            return;
        }

        ValidateCircleSides(nameof(FillCircle), numSides);

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        switch (context)
        {
            case DrawContext.Sprites:
                Texture2D circleTexture = _owner.Renderer.GetOrCreateWhiteCircleTexture(radius, null, null);
                float scale = radius * 2 / circleTexture.Width;
                _owner.DrawTextureAt(circleTexture, null, center - new Vector2(radius), color, Vector2.Zero, 0, scale, scale);
                break;

            case DrawContext.Primitives:
                _owner.PrimitiveDrawing.DrawSolidEllipse(center, new Vector2(radius), numSides, color, false);
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {context}");
        }
    }

    public void FillEllipse(Vector2 center, float radiusX, float radiusY, Color color, int numSides = 32, DrawContext? preferredContext = null)
    {
        if (radiusX <= 0.0f || radiusX.IsAlmostZero() || radiusY <= 0.0f || radiusY.IsAlmostZero())
        {
            return;
        }

        ValidateCircleSides(nameof(FillEllipse), numSides);
        _owner.EnsureDrawContext(DrawContext.Primitives);
        _owner.PrimitiveDrawing.DrawSolidEllipse(center, new Vector2(radiusX, radiusY), numSides, color, false);
    }

    public void StrokeEllipse(Vector2 center, float radiusX, float radiusY, Color color, float thickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
    {
        if (radiusX <= 0.0f || radiusX.IsAlmostZero() || radiusY <= 0.0f || radiusY.IsAlmostZero() || thickness <= 0.0f || thickness.IsAlmostZero())
        {
            return;
        }

        ValidateCircleSides(nameof(StrokeEllipse), numSides);

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        Vector2[] vertices = CasaShapeGeometry.GetEllipseVertices(center, radiusX, radiusY, numSides);
        for (int i = 0; i < numSides; i++)
        {
            StrokeLineSegment(Vector2.Zero, vertices[i], vertices[(i + 1) % numSides], color, thickness, context);
        }
    }

    public void StrokeAndFillEllipse(Vector2 center, float radiusX, float radiusY, Color strokeColor, Color fillColor, float strokeThickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
    {
        if (radiusX <= 0.0f || radiusX.IsAlmostZero() || radiusY <= 0.0f || radiusY.IsAlmostZero())
        {
            return;
        }

        if (radiusX.IsAlmostEqual(strokeThickness) || radiusY.IsAlmostEqual(strokeThickness))
        {
            FillEllipse(center, radiusX, radiusY, strokeColor, numSides, preferredContext);
            return;
        }

        FillEllipse(center, radiusX, radiusY, fillColor, numSides, preferredContext);
        StrokeEllipse(center, radiusX, radiusY, strokeColor, strokeThickness, numSides, preferredContext);
    }

    public void StrokeAndFillPolygon(Vector2 origin, IReadOnlyList<Vector2> vertices, Color strokeColor, Color fillColor, float strokeThickness = 1.0f, bool centerLinesOnVertices = true, WindingOrder? order = null)
    {
        if (vertices == null || !vertices.Any())
        {
            throw new ArgumentException(null, nameof(vertices));
        }

        if (!centerLinesOnVertices && strokeColor == fillColor)
        {
            FillPolygon(origin, vertices, strokeColor);
            return;
        }

        FillPolygon(origin, vertices, fillColor);
        StrokePolygon(origin, vertices, strokeColor, strokeThickness, centerLinesOnVertices, order, DrawContext.Primitives);
    }

    public void StrokePolygon(Vector2 origin, IReadOnlyList<Vector2> vertices, Color color, float thickness = 1.0f, bool centerLinesOnVertices = true, WindingOrder? order = null, DrawContext? preferredContext = null)
    {
        if (thickness <= 0.0f || thickness.IsAlmostZero())
        {
            return;
        }

        if (vertices == null || !vertices.Any())
        {
            throw new ArgumentException(null, nameof(vertices));
        }

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        if (centerLinesOnVertices)
        {
            IEnumerable<(Vector2 Start, Vector2 End)> edges = vertices.SelectConsecutivePairs(true);
            foreach ((Vector2 Start, Vector2 End) in edges)
            {
                StrokeLineSegment(origin, Start, End, color, thickness, context);
            }

            return;
        }

        order ??= Triangulator.DetermineWindingOrder(vertices.Append(vertices[0]).ToArray());

        switch (context)
        {
            case DrawContext.Sprites:
                _owner.SpriteBatch.DrawPolygon(origin, Triangulator.EnsureWindingOrder(vertices.ToArray(), WindingOrder.CounterClockwise), color, thickness, 0.0f);
                break;

            case DrawContext.Primitives:
                IEnumerable<(Vector2 Start, Vector2 End)> edges = vertices.SelectConsecutivePairs(true);
                switch (order.Value)
                {
                    case WindingOrder.Clockwise:
                        foreach ((Vector2 Start, Vector2 End) in edges)
                        {
                            Vector2 offset = (End - Start).RightNormal().NormalizedCopy().Scale(thickness);
                            FillTrianglePrimitive(origin, Start, End, End + offset, color);
                            FillTrianglePrimitive(origin, End + offset, Start + offset, Start, color);
                        }
                        break;

                    case WindingOrder.CounterClockwise:
                        foreach ((Vector2 Start, Vector2 End) in edges)
                        {
                            Vector2 offset = (End - Start).LeftNormal().NormalizedCopy().Scale(thickness);
                            FillTrianglePrimitive(origin, Start, End, End + offset, color);
                            FillTrianglePrimitive(origin, End + offset, Start + offset, Start, color);
                        }
                        break;

                    default:
                        throw new NotImplementedException($"Unrecognized {nameof(WindingOrder)}: {order}");
                }
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {context}");
        }
    }

    public void FillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color color)
    {
        if (vertices == null || !vertices.Any())
        {
            throw new ArgumentException(null, nameof(vertices));
        }

        _owner.EnsureDrawContext(DrawContext.Primitives);
        _owner.PrimitiveDrawing.DrawSolidPolygon(origin, vertices.ToArray(), color, false);
    }

    public void StrokeAndFillPoint(Vector2 position, Color strokeColor, Color fillColor, float radius = 3.0f, int strokeThickness = 1, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null)
    {
        if (radius.IsAlmostEqual(strokeThickness))
        {
            FillPoint(position, strokeColor, radius, shape, preferredContext);
            return;
        }

        FillPoint(position, fillColor, radius, shape, preferredContext);
        StrokePoint(position, strokeColor, radius, strokeThickness, shape, preferredContext);
    }

    public void StrokePoint(Vector2 position, Color color, float radius = 1.0f, int thickness = 1, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null)
    {
        if (radius <= 0.0f || radius.IsAlmostZero() || thickness <= 0)
        {
            return;
        }

        if (radius.IsAlmostEqual(thickness))
        {
            FillPoint(position, color, radius, shape, preferredContext);
            return;
        }

        switch (shape)
        {
            case CasaDrawTransaction.PointShape.Circle:
                StrokeCircle(position, color, radius, thickness, 32, preferredContext);
                break;

            case CasaDrawTransaction.PointShape.Square:
                StrokeRectangle(Vector2.Zero, new RectangleF(position.X - radius, position.Y - radius, radius * 2, radius * 2), color, new Thickness(thickness), preferredContext);
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(CasaDrawTransaction.PointShape)}: {shape}");
        }
    }

    public void FillPoint(Vector2 position, Color color, float radius = 1.0f, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null)
    {
        if (radius <= 0.0f || radius.IsAlmostZero())
        {
            return;
        }

        switch (shape)
        {
            case CasaDrawTransaction.PointShape.Circle:
                FillCircle(position, color, radius, 32, preferredContext);
                break;

            case CasaDrawTransaction.PointShape.Square:
                FillRectangle(Vector2.Zero, new RectangleF(position.X - radius, position.Y - radius, radius * 2, radius * 2), color, preferredContext);
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(CasaDrawTransaction.PointShape)}: {shape}");
        }
    }

    public void StrokeLineSegment(Vector2 origin, Vector2 start, Vector2 end, Color color, float thickness = 1.0f, DrawContext? preferredContext = null)
    {
        if (start.IsAlmostEqualTo(end) || thickness <= 0.0f || thickness.IsAlmostZero())
        {
            return;
        }

        DrawContext context = _owner.ResolveDrawContext(preferredContext, DrawContext.Sprites);
        _owner.EnsureDrawContext(context);

        switch (context)
        {
            case DrawContext.Sprites:
                _owner.SpriteBatch.DrawLine(origin + start, origin + end, color, thickness, 0.0f);
                break;

            case DrawContext.Primitives:
                Vector2 offset = (end - start).LeftNormal().NormalizedCopy().Scale(thickness / 2f);
                FillTrianglePrimitive(origin, start - offset, end + offset, end - offset, color);
                FillTrianglePrimitive(origin, start + offset, end + offset, start - offset, color);
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {context}");
        }
    }

    public void FillTriangle(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
        => _owner.FillTriangleCore(origin, v0, c0, v1, c1, v2, c2);

    public void FillQuadrilateralLinearClamp(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2, Vector2 v3, Color c3)
        => _owner.FillQuadrilateralLinearClampCore(origin, v0, c0, v1, c1, v2, c2, v3, c3);

    private void FillTrianglePrimitive(Vector2 origin, Vector2 v0, Vector2 v1, Vector2 v2, Color color)
        => _owner.FillTrianglePrimitiveCore(origin, v0, v1, v2, color);

    private static void ValidateCircleSides(string methodName, int numSides)
    {
        if (numSides > CircleMaxSides)
        {
            throw new ArgumentException($"{methodName}.{nameof(numSides)} cannot exceed {CircleMaxSides}.");
        }
    }
}