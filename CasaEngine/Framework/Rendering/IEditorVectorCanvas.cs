using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

public interface IEditorVectorCanvas : IDisposable
{
    IVectorCanvasSession Begin(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame);
}

public interface IVectorCanvasSession : IDisposable
{
    void SaveState();
    void RestoreState();
    void ResetState();
    void GlobalAlpha(float alpha);
    void Translate(float x, float y);
    void Scale(float x, float y);
    void Rotate(float radians);

    void BeginPath();
    void MoveTo(float x, float y);
    void LineTo(float x, float y);
    void Rect(float x, float y, float width, float height);
    void RoundedRect(float x, float y, float width, float height, float radius);
    void Circle(float x, float y, float radius);

    void FillColor(Color color);
    void StrokeColor(Color color);
    void StrokeWidth(float width);
    void Fill();
    void Stroke();

    void Scissor(float x, float y, float width, float height);
    void IntersectScissor(float x, float y, float width, float height);
    void ResetScissor();

    void DrawText(string text, float x, float y, Color color, float fontSize);
}

public sealed class NullEditorVectorCanvas : IEditorVectorCanvas
{
    public static NullEditorVectorCanvas Instance { get; } = new();

    private static NullVectorCanvasSession Session { get; } = new();

    private NullEditorVectorCanvas()
    {
    }

    public IVectorCanvasSession Begin(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
        => Session;

    public void Dispose()
    {
    }

    private sealed class NullVectorCanvasSession : IVectorCanvasSession
    {
        public void SaveState()
        {
        }

        public void RestoreState()
        {
        }

        public void ResetState()
        {
        }

        public void GlobalAlpha(float alpha)
        {
        }

        public void Translate(float x, float y)
        {
        }

        public void Scale(float x, float y)
        {
        }

        public void Rotate(float radians)
        {
        }

        public void BeginPath()
        {
        }

        public void MoveTo(float x, float y)
        {
        }

        public void LineTo(float x, float y)
        {
        }

        public void Rect(float x, float y, float width, float height)
        {
        }

        public void RoundedRect(float x, float y, float width, float height, float radius)
        {
        }

        public void Circle(float x, float y, float radius)
        {
        }

        public void FillColor(Color color)
        {
        }

        public void StrokeColor(Color color)
        {
        }

        public void StrokeWidth(float width)
        {
        }

        public void Fill()
        {
        }

        public void Stroke()
        {
        }

        public void Scissor(float x, float y, float width, float height)
        {
        }

        public void IntersectScissor(float x, float y, float width, float height)
        {
        }

        public void ResetScissor()
        {
        }

        public void DrawText(string text, float x, float y, Color color, float fontSize)
        {
        }

        public void Dispose()
        {
        }
    }
}