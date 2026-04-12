using System;
using System.IO;
using FontStashSharp;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NvgSharp;

namespace CasaEngine.Editor.Rendering.Vector;

internal sealed class NvgSharpVectorCanvas : IEditorVectorCanvas
{
    private readonly string _defaultFontPath;
    private NvgContext? _context;
    private FontSystem? _defaultFontSystem;

    public NvgSharpVectorCanvas(string defaultFontPath)
    {
        if (string.IsNullOrWhiteSpace(defaultFontPath))
        {
            throw new ArgumentException("A default font path is required for the vector canvas.", nameof(defaultFontPath));
        }

        _defaultFontPath = defaultFontPath;
    }

    public IVectorCanvasSession Begin(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(view);

        _context ??= new NvgContext(graphicsDevice, true, true);
        _context.DevicePixelRatio = 1.0f;
        _context.ResetState();
        _context.SaveState();
        _context.Scissor(frame.ViewportRect.X, frame.ViewportRect.Y, frame.ViewportRect.Width, frame.ViewportRect.Height);

        return new Session(_context, GetDefaultFontSystem());
    }

    public void Dispose()
    {
        _defaultFontSystem = null;
        _context = null;
    }

    private FontSystem GetDefaultFontSystem()
    {
        if (_defaultFontSystem != null)
        {
            return _defaultFontSystem;
        }

        var fontSystem = new FontSystem();
        using (FileStream stream = File.OpenRead(_defaultFontPath))
        {
            fontSystem.AddFont(stream);
        }

        _defaultFontSystem = fontSystem;
        return fontSystem;
    }

    private sealed class Session : IVectorCanvasSession
    {
        private readonly NvgContext _context;
        private readonly FontSystem _fontSystem;
        private bool _disposed;

        public Session(NvgContext context, FontSystem fontSystem)
        {
            _context = context;
            _fontSystem = fontSystem;
        }

        public void SaveState() => _context.SaveState();

        public void RestoreState() => _context.RestoreState();

        public void ResetState() => _context.ResetState();

        public void GlobalAlpha(float alpha) => _context.GlobalAlpha(alpha);

        public void Translate(float x, float y) => _context.Translate(x, y);

        public void Scale(float x, float y) => _context.Scale(x, y);

        public void Rotate(float radians) => _context.Rotate(radians);

        public void BeginPath() => _context.BeginPath();

        public void MoveTo(float x, float y) => _context.MoveTo(x, y);

        public void LineTo(float x, float y) => _context.LineTo(x, y);

        public void Rect(float x, float y, float width, float height) => _context.Rect(x, y, width, height);

        public void RoundedRect(float x, float y, float width, float height, float radius) => _context.RoundedRect(x, y, width, height, radius);

        public void Circle(float x, float y, float radius) => _context.Circle(x, y, radius);

        public void FillColor(Color color) => _context.FillColor(color);

        public void StrokeColor(Color color) => _context.StrokeColor(color);

        public void StrokeWidth(float width) => _context.StrokeWidth(width);

        public void Fill() => _context.Fill();

        public void Stroke() => _context.Stroke();

        public void Scissor(float x, float y, float width, float height) => _context.Scissor(x, y, width, height);

        public void IntersectScissor(float x, float y, float width, float height) => _context.IntersectScissor(x, y, width, height);

        public void ResetScissor() => _context.ResetScissor();

        public void DrawText(string text, float x, float y, Color color, float fontSize)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _context.FillColor(color);
            _context.Text(_fontSystem.GetFont(Math.Max(1, (int)MathF.Round(fontSize))), text, x, y);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _context.RestoreState();
            _context.Flush();
        }
    }
}