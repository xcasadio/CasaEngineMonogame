using System.Runtime.CompilerServices;
using Apos.Shapes;
using CasaEngine.Framework.UI.Backend.MonoGame;
using CasaEngine.Framework.UI.Backend.MonoGame.Primitives;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Triangulation;
using WindingOrder = MonoGame.Extended.Triangulation.WindingOrder;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Primitives;

public sealed class CasaAposShapeRenderer2D : IShapeRenderer2D
{
    private sealed class ShapeBatchHolder
    {
        public ShapeBatchHolder(ShapeBatch shapeBatch)
        {
            ShapeBatch = shapeBatch;
        }

        public ShapeBatch ShapeBatch { get; }
    }

    private static readonly ConditionalWeakTable<CasaDesktopRuntime, ShapeBatchHolder> ShapeBatchCache = new();

    private readonly CasaDrawTransaction _owner;
    private readonly IShapeRenderer2D _fallback;
    private readonly ShapeBatch _shapeBatch;
    private int _batchDepth;

    public CasaAposShapeRenderer2D(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
        _fallback = new CasaLegacyShapeRenderer2D(owner);
        _shapeBatch = ShapeBatchCache.GetValue(owner.Renderer, static renderer =>
            new ShapeBatchHolder(new ShapeBatch(renderer.GraphicsDevice, renderer.Content))).ShapeBatch;
    }

    public void StrokeAndFillRectangle(Vector2 origin, RectangleF destination, Color strokeColor, Color fillColor, Thickness strokeThickness, DrawContext? preferredContext = null)
    {
        if (!TryGetUniformThickness(strokeThickness, out float thickness))
        {
            _fallback.StrokeAndFillRectangle(origin, destination, strokeColor, fillColor, strokeThickness, preferredContext);
            return;
        }

        Execute(() => _shapeBatch.DrawRectangle(origin + destination.Position, new Vector2(destination.Width, destination.Height), fillColor, strokeColor, thickness));
    }

    public void StrokeRectangle(Vector2 origin, RectangleF destination, Color color, Thickness thickness, DrawContext? preferredContext = null)
    {
        if (!TryGetUniformThickness(thickness, out float uniformThickness))
        {
            _fallback.StrokeRectangle(origin, destination, color, thickness, preferredContext);
            return;
        }

        Execute(() => _shapeBatch.BorderRectangle(origin + destination.Position, new Vector2(destination.Width, destination.Height), color, uniformThickness));
    }

    public void FillRectangle(Vector2 origin, RectangleF destination, Color color, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.FillRectangle(origin + destination.Position, new Vector2(destination.Width, destination.Height), color));

    public void StrokeAndFillCircle(Vector2 center, Color strokeColor, Color fillColor, float radius, float strokeThickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.DrawCircle(center, radius, fillColor, strokeColor, strokeThickness));

    public void StrokeCircle(Vector2 center, Color color, float radius, float thickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.BorderCircle(center, radius, color, thickness));

    public void FillCircle(Vector2 center, Color color, float radius, int numSides = 32, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.FillCircle(center, radius, color));

    public void FillEllipse(Vector2 center, float radiusX, float radiusY, Color color, int numSides = 32, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.FillEllipse(center, radiusX, radiusY, color));

    public void StrokeEllipse(Vector2 center, float radiusX, float radiusY, Color color, float thickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.BorderEllipse(center, radiusX, radiusY, color, thickness));

    public void StrokeAndFillEllipse(Vector2 center, float radiusX, float radiusY, Color strokeColor, Color fillColor, float strokeThickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.DrawEllipse(center, radiusX, radiusY, fillColor, strokeColor, strokeThickness));

    public void StrokeAndFillPolygon(Vector2 origin, IReadOnlyList<Vector2> vertices, Color strokeColor, Color fillColor, float strokeThickness = 1.0f, bool centerLinesOnVertices = true, WindingOrder? order = null)
        => _fallback.StrokeAndFillPolygon(origin, vertices, strokeColor, fillColor, strokeThickness, centerLinesOnVertices, order);

    public void StrokePolygon(Vector2 origin, IReadOnlyList<Vector2> vertices, Color color, float thickness = 1.0f, bool centerLinesOnVertices = true, WindingOrder? order = null, DrawContext? preferredContext = null)
        => _fallback.StrokePolygon(origin, vertices, color, thickness, centerLinesOnVertices, order, preferredContext);

    public void FillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color color)
        => _fallback.FillPolygon(origin, vertices, color);

    public void StrokeAndFillPoint(Vector2 position, Color strokeColor, Color fillColor, float radius = 3.0f, int strokeThickness = 1, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null)
    {
        if (shape == CasaDrawTransaction.PointShape.Circle)
        {
            Execute(() => _shapeBatch.DrawCircle(position, radius, fillColor, strokeColor, strokeThickness));
            return;
        }

        _fallback.StrokeAndFillPoint(position, strokeColor, fillColor, radius, strokeThickness, shape, preferredContext);
    }

    public void StrokePoint(Vector2 position, Color color, float radius = 1.0f, int thickness = 1, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null)
    {
        if (shape == CasaDrawTransaction.PointShape.Circle)
        {
            Execute(() => _shapeBatch.BorderCircle(position, radius, color, thickness));
            return;
        }

        _fallback.StrokePoint(position, color, radius, thickness, shape, preferredContext);
    }

    public void FillPoint(Vector2 position, Color color, float radius = 1.0f, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null)
    {
        if (shape == CasaDrawTransaction.PointShape.Circle)
        {
            Execute(() => _shapeBatch.FillCircle(position, radius, color));
            return;
        }

        _fallback.FillPoint(position, color, radius, shape, preferredContext);
    }

    public void StrokeLineSegment(Vector2 origin, Vector2 start, Vector2 end, Color color, float thickness = 1.0f, DrawContext? preferredContext = null)
        => Execute(() => _shapeBatch.FillLine(origin + start, origin + end, thickness * 0.5f, color));

    public void FillTriangle(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
    {
        if (c0 == c1 && c1 == c2)
        {
            Execute(() => _shapeBatch.FillTriangle(origin + v0, origin + v1, origin + v2, c0));
            return;
        }

        _fallback.FillTriangle(origin, v0, c0, v1, c1, v2, c2);
    }

    public void FillQuadrilateralLinearClamp(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2, Vector2 v3, Color c3)
        => _fallback.FillQuadrilateralLinearClamp(origin, v0, c0, v1, c1, v2, c2, v3, c3);

    private void Execute(Action drawAction)
    {
        ArgumentNullException.ThrowIfNull(drawAction);

        bool beginBatch = _batchDepth == 0;
        if (beginBatch)
        {
            _owner.EndCurrentContext();
            _shapeBatch.Begin(
                _owner.CurrentSettings.Transform,
                Matrix.CreateOrthographicOffCenter(0, _owner.GraphicsDevice.Viewport.Width, _owner.GraphicsDevice.Viewport.Height, 0, 0, 1),
                CasaMonoGameRenderInterop.GetBlendState(_owner.CurrentSettings),
                CasaMonoGameRenderInterop.GetSamplerState(_owner.CurrentSettings),
                CasaMonoGameRenderInterop.GetDepthStencilState(_owner.CurrentSettings),
                CasaMonoGameRenderInterop.GetRasterizerState(_owner.CurrentSettings));
        }

        _batchDepth++;
        try
        {
            drawAction();
        }
        finally
        {
            _batchDepth--;
            if (beginBatch)
            {
                _shapeBatch.End();
            }
        }
    }

    private static bool TryGetUniformThickness(Thickness thickness, out float uniformThickness)
    {
        uniformThickness = thickness.Left;
        return thickness.Left == thickness.Top
            && thickness.Left == thickness.Right
            && thickness.Left == thickness.Bottom;
    }
}