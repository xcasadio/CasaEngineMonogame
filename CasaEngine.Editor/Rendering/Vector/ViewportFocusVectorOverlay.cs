using System;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Rendering.Vector;

internal sealed class ViewportFocusVectorOverlay
{
    public void Draw(
        IEditorVectorCanvas vectorCanvas,
        GraphicsDevice graphicsDevice,
        RenderView view,
        in RenderFrame frame,
        bool receivesPointerInput,
        bool isKeyboardFocused)
    {
        ArgumentNullException.ThrowIfNull(vectorCanvas);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(view);

        if (!receivesPointerInput && !isKeyboardFocused)
        {
            return;
        }

        Rectangle viewport = frame.ViewportRect;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        using IVectorCanvasSession session = vectorCanvas.Begin(graphicsDevice, view, in frame);

        float inset = isKeyboardFocused ? 5f : 7f;
        float x = viewport.X + inset;
        float y = viewport.Y + inset;
        float width = Math.Max(0, viewport.Width - inset * 2f);
        float height = Math.Max(0, viewport.Height - inset * 2f);
        float radius = isKeyboardFocused ? 12f : 10f;

        Color borderColor = isKeyboardFocused
            ? new Color(255, 196, 76, 220)
            : new Color(92, 201, 255, 180);
        Color badgeFillColor = isKeyboardFocused
            ? new Color(52, 33, 8, 196)
            : new Color(12, 41, 58, 180);

        session.BeginPath();
        session.RoundedRect(x, y, width, height, radius);
        session.StrokeColor(borderColor);
        session.StrokeWidth(isKeyboardFocused ? 3.0f : 2.0f);
        session.Stroke();

        const float badgeWidth = 108f;
        const float badgeHeight = 28f;
        float badgeX = viewport.X + 14f;
        float badgeY = viewport.Y + 14f;

        session.BeginPath();
        session.RoundedRect(badgeX, badgeY, badgeWidth, badgeHeight, 14f);
        session.FillColor(badgeFillColor);
        session.Fill();

        session.BeginPath();
        session.RoundedRect(badgeX, badgeY, badgeWidth, badgeHeight, 14f);
        session.StrokeColor(borderColor);
        session.StrokeWidth(1.5f);
        session.Stroke();

        string label = isKeyboardFocused ? "Viewport Input" : "Viewport Hover";
        session.DrawText(label, badgeX + 12f, badgeY + 18f, Color.White, 14f);
    }
}