#nullable enable

using System;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Editor.Controls.Timeline.Rendering;

public sealed class DefaultTimelineItemRenderer : ITimelineItemRenderer
{
    public void DrawItem(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        TimelineItemVisualState state)
    {
        Vector2 origin = drawArgs.Offset.ToVector2();
        float opacity = drawArgs.Opacity;
        bool selected = (state & TimelineItemVisualState.Selected) != 0;
        bool hovered = (state & TimelineItemVisualState.Hovered) != 0;

        Color fillColor = selected
            ? EditorThemePalette.AccentSelection * opacity
            : hovered
                ? Color.White * opacity
                : Color.Gainsboro * (0.9f * opacity);
        Color strokeColor = selected
            ? EditorThemePalette.InlineRenameBorder * opacity
            : hovered
                ? EditorThemePalette.AccentSelection * opacity
                : EditorThemePalette.PanelBorder * opacity;

        switch (item.Kind)
        {
            case TimelineItemKind.Duration:
                DrawBlock(drawArgs, origin, context, item, bounds, fillColor, strokeColor, opacity);
                break;

            case TimelineItemKind.Range:
                DrawRange(drawArgs, origin, bounds, fillColor, strokeColor);
                break;

            case TimelineItemKind.Marker:
                drawArgs.Context.StrokeLineSegment(origin, new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Left, bounds.Bottom), strokeColor, 1.5f);
                break;

            default:
                DrawDiamond(drawArgs, origin, bounds, fillColor, strokeColor);
                break;
        }
    }

    public bool HitTest(
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        Point mousePosition)
    {
        return mousePosition.X >= bounds.Left
            && mousePosition.X <= bounds.Right
            && mousePosition.Y >= bounds.Top
            && mousePosition.Y <= bounds.Bottom;
    }

    private static void DrawDiamond(ElementDrawArgs DA, Vector2 origin, RectangleF bounds, Color fillColor, Color strokeColor)
    {
        float x = bounds.Center.X;
        float centerY = bounds.Center.Y;
        float half = Math.Min(TimelineControlMetrics.ItemHalfSize, Math.Max(3f, (bounds.Height * 0.5f) - 1f));

        Vector2 top = new(x, centerY - half);
        Vector2 right = new(x + half, centerY);
        Vector2 bottom = new(x, centerY + half);
        Vector2 left = new(x - half, centerY);

        DA.Context.FillTriangle(origin, top, fillColor, right, fillColor, bottom, fillColor);
        DA.Context.FillTriangle(origin, top, fillColor, bottom, fillColor, left, fillColor);

        DA.Context.StrokeLineSegment(origin, top, right, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, right, bottom, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, bottom, left, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, left, top, strokeColor, 1f);
    }

    private static void DrawBlock(ElementDrawArgs DA, Vector2 origin, TimelineRenderContext context, TimelineItem item, RectangleF bounds, Color fillColor, Color strokeColor, float opacity)
    {
        float top = bounds.Top + 3f;
        float bottom = bounds.Bottom - 3f;
        float left = bounds.Left;
        float right = Math.Max(bounds.Left + 2f, bounds.Right);
        float width = right - left;
        float height = Math.Max(6f, bottom - top);

        DA.Context.FillRectangle(origin, new RectangleF(left, top, width, height), fillColor * 0.55f);
        DrawRectBorder(DA, origin, left, top, right, top + height, strokeColor);

        string label = string.IsNullOrEmpty(item.DisplayName) ? item.ItemType : item.DisplayName;
        ResolvedFont font = context.Font;
        if (string.IsNullOrEmpty(label) || context.TextEngine == null || !font.IsAvailable)
        {
            return;
        }

        Vector2 textSize = context.TextEngine.MeasureText(font, label);
        if (textSize.X > width - 6f)
        {
            return;
        }

        float textHeight = Math.Max(textSize.Y, font.LineHeight * context.FontScale);
        float labelX = left + 4f;
        float labelY = top + Math.Max(0f, (height - textHeight) * 0.5f);
        Vector2 drawPosition = new Vector2(labelX, labelY) + (font.DrawOrigin * context.FontScale) + origin;
        DA.DT.DrawTextViaEngine(font, label, drawPosition, Color.Black * opacity, font.DrawOrigin, context.FontScale);
    }

    private static void DrawRange(ElementDrawArgs DA, Vector2 origin, RectangleF bounds, Color fillColor, Color strokeColor)
    {
        float left = bounds.Left;
        float right = Math.Max(bounds.Left + 2f, bounds.Right);
        DA.Context.FillRectangle(origin, new RectangleF(left, bounds.Top + 2f, right - left, Math.Max(4f, bounds.Height - 4f)), fillColor * 0.25f);
        DA.Context.StrokeLineSegment(origin, new Vector2(left, bounds.Top), new Vector2(left, bounds.Bottom), strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, new Vector2(right, bounds.Top), new Vector2(right, bounds.Bottom), strokeColor, 1f);
    }

    private static void DrawRectBorder(ElementDrawArgs DA, Vector2 origin, float left, float top, float right, float bottom, Color strokeColor)
    {
        Vector2 topLeft = new(left, top);
        Vector2 topRight = new(right, top);
        Vector2 bottomRight = new(right, bottom);
        Vector2 bottomLeft = new(left, bottom);

        DA.Context.StrokeLineSegment(origin, topLeft, topRight, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, topRight, bottomRight, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, bottomRight, bottomLeft, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, bottomLeft, topLeft, strokeColor, 1f);
    }
}
