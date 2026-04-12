using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Triangulation;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Primitives;

public interface IShapeRenderer2D
{
    void StrokeAndFillRectangle(Vector2 origin, RectangleF destination, Color strokeColor, Color fillColor, Thickness strokeThickness, DrawContext? preferredContext = null);
    void StrokeRectangle(Vector2 origin, RectangleF destination, Color color, Thickness thickness, DrawContext? preferredContext = null);
    void FillRectangle(Vector2 origin, RectangleF destination, Color color, DrawContext? preferredContext = null);

    void StrokeAndFillCircle(Vector2 center, Color strokeColor, Color fillColor, float radius, float strokeThickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null);
    void StrokeCircle(Vector2 center, Color color, float radius, float thickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null);
    void FillCircle(Vector2 center, Color color, float radius, int numSides = 32, DrawContext? preferredContext = null);

    void FillEllipse(Vector2 center, float radiusX, float radiusY, Color color, int numSides = 32, DrawContext? preferredContext = null);
    void StrokeEllipse(Vector2 center, float radiusX, float radiusY, Color color, float thickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null);
    void StrokeAndFillEllipse(Vector2 center, float radiusX, float radiusY, Color strokeColor, Color fillColor, float strokeThickness = 1.0f, int numSides = 32, DrawContext? preferredContext = null);

    void StrokeAndFillPolygon(Vector2 origin, IReadOnlyList<Vector2> vertices, Color strokeColor, Color fillColor, float strokeThickness = 1.0f, bool centerLinesOnVertices = true, WindingOrder? order = null);
    void StrokePolygon(Vector2 origin, IReadOnlyList<Vector2> vertices, Color color, float thickness = 1.0f, bool centerLinesOnVertices = true, WindingOrder? order = null, DrawContext? preferredContext = null);
    void FillPolygon(Vector2 origin, IEnumerable<Vector2> vertices, Color color);

    void StrokeAndFillPoint(Vector2 position, Color strokeColor, Color fillColor, float radius = 3.0f, int strokeThickness = 1, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null);
    void StrokePoint(Vector2 position, Color color, float radius = 1.0f, int thickness = 1, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null);
    void FillPoint(Vector2 position, Color color, float radius = 1.0f, CasaDrawTransaction.PointShape shape = CasaDrawTransaction.PointShape.Circle, DrawContext? preferredContext = null);

    void StrokeLineSegment(Vector2 origin, Vector2 start, Vector2 end, Color color, float thickness = 1.0f, DrawContext? preferredContext = null);
    void FillTriangle(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2);
    void FillQuadrilateralLinearClamp(Vector2 origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2, Vector2 v3, Color c3);
}