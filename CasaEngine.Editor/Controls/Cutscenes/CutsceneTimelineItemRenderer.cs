#nullable enable

using System;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.Controls.Timeline.Rendering;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.Cutscenes;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls.Cutscenes;

/// <summary>
/// What the renderer needs to know about an item of the cutscene timeline. It is the <see cref="TimelineItem.Source"/> of
/// the item, built when the model is rebuilt: label widths are measured on first draw and kept until the next rebuild.
/// </summary>
internal sealed class CutsceneTimelineItemInfo
{
    public CutsceneTimelineItemInfo(
        CutsceneTimelineItem item,
        CutsceneTimelineRowKind rowKind,
        string shortLabel,
        double nextStartSeconds,
        float labelInset,
        double labelEndSeconds,
        bool canDrag = false,
        float lowestInstantSeconds = 0f)
    {
        Item = item;
        RowKind = rowKind;
        ShortLabel = shortLabel;
        NextStartSeconds = nextStartSeconds;
        LabelInset = labelInset;
        LabelEndSeconds = labelEndSeconds;
        CanDrag = canDrag;
        LowestInstantSeconds = lowestInstantSeconds;
    }

    public CutsceneTimelineItem Item { get; }

    public CutsceneTimelineRowKind RowKind { get; }

    /// <summary>The type of the action, used when the full label does not fit.</summary>
    public string ShortLabel { get; }

    /// <summary>Start of the next item on the same track, or infinity: the room a marker label may use.</summary>
    public double NextStartSeconds { get; }

    /// <summary>Space kept free at the left of a bar label, for a marker drawn on the start of the bar.</summary>
    public float LabelInset { get; }

    /// <summary>Where the label of a bar must end: the end of the bar, or the first marker drawn over it.</summary>
    public double LabelEndSeconds { get; }

    /// <summary>Whether the item can be dragged in time (P5): its left edge is drawn lighter (P8).</summary>
    public bool CanDrag { get; }

    /// <summary>For an item that can be dragged: the lowest time it can be dragged to, where the delay of its At would be 0 (P6).</summary>
    public float LowestInstantSeconds { get; }

    public float LabelWidth { get; set; } = -1f;

    /// <summary>The truncated label last built, and the number of characters it keeps.</summary>
    public string? TruncatedLabel { get; set; }

    public int TruncatedLength { get; set; } = -1;

    public float ShortLabelWidth { get; set; } = -1f;
}

/// <summary>
/// Draws the items of the cutscene timeline (ADR-0137): a solid bar for a known duration, a pale bar with a dashed outline for
/// an estimated one, a bar open on the right for an action that waits for an input, a translucent range for a loop and a
/// diamond with its label for an instant. Labels are drawn only where they fit, so they never overlap.
/// </summary>
internal sealed class CutsceneTimelineItemRenderer : ITimelineItemRenderer
{
    private const float BarInset = 5f;
    private const float DashLength = 4f;
    private const float DashGap = 3f;
    private const float MarkerHalfSize = 6f;
    private const float LabelPadding = 5f;
    private const float GripWidth = 3f;

    public void DrawItem(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        TimelineItemVisualState state)
    {
        if (item.Source is not CutsceneTimelineItemInfo info)
        {
            return;
        }

        Vector2 origin = drawArgs.Offset.ToVector2();
        float opacity = drawArgs.Opacity;
        bool selected = (state & TimelineItemVisualState.Selected) != 0;
        bool hovered = (state & TimelineItemVisualState.Hovered) != 0;
        Color baseColor = GetBaseColor(info.RowKind);

        switch (info.Item.Shape)
        {
            case CutsceneTimelineItemShape.Range:
                DrawRange(drawArgs, context, origin, info, item, bounds, baseColor, selected, hovered, opacity);
                break;

            case CutsceneTimelineItemShape.Marker:
                DrawMarker(drawArgs, context, origin, info, item, bounds, baseColor, selected, hovered, opacity);
                break;

            default:
                DrawBar(drawArgs, context, origin, info, item, bounds, baseColor, selected, hovered, opacity);
                break;
        }
    }

    public bool HitTest(TimelineRenderContext context, TimelineTrack track, TimelineItem item, RectangleF bounds, Point mousePosition)
    {
        return mousePosition.X >= bounds.Left
            && mousePosition.X <= bounds.Right
            && mousePosition.Y >= bounds.Top
            && mousePosition.Y <= bounds.Bottom;
    }

    private static Color GetBaseColor(CutsceneTimelineRowKind kind)
    {
        return kind switch
        {
            CutsceneTimelineRowKind.Loops => new Color(150, 112, 214),
            CutsceneTimelineRowKind.Flow => new Color(124, 134, 150),
            CutsceneTimelineRowKind.Entity => new Color(66, 140, 224),
            CutsceneTimelineRowKind.Screen => new Color(226, 146, 62),
            CutsceneTimelineRowKind.Audio => new Color(84, 184, 118),
            CutsceneTimelineRowKind.WorldAndInput => new Color(206, 92, 120),
            _ => new Color(150, 150, 150),
        };
    }

    private static void DrawBar(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        Vector2 origin,
        CutsceneTimelineItemInfo info,
        TimelineItem item,
        RectangleF bounds,
        Color baseColor,
        bool selected,
        bool hovered,
        float opacity)
    {
        float top = bounds.Top + BarInset;
        float bottom = bounds.Bottom - BarInset;
        float left = bounds.Left;
        float right = Math.Max(bounds.Left + 3f, bounds.Right);
        float width = right - left;
        float height = bottom - top;
        bool estimated = info.Item.IsEstimated && !info.Item.IsMeasured;
        bool open = info.Item.Shape == CutsceneTimelineItemShape.OpenBar;

        float fillAlpha = estimated ? 0.32f : 0.9f;
        if (hovered || selected)
        {
            fillAlpha = Math.Min(1f, fillAlpha + 0.12f);
        }

        drawArgs.Context.FillRectangle(origin, new RectangleF(left, top, width, height), baseColor * (fillAlpha * opacity));

        Color border = selected
            ? Color.White * opacity
            : estimated
                ? baseColor * opacity
                : Color.Lerp(baseColor, Color.Black, 0.35f) * opacity;
        float thickness = selected ? 2f : 1f;

        if (estimated)
        {
            DrawDashedLine(drawArgs, origin, left, right, top, border, thickness);
            DrawDashedLine(drawArgs, origin, left, right, bottom, border, thickness);
            drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, top), new Vector2(left, bottom), border, thickness);
            if (!open)
            {
                drawArgs.Context.StrokeLineSegment(origin, new Vector2(right, top), new Vector2(right, bottom), border, thickness);
            }
        }
        else
        {
            drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, top), new Vector2(right, top), border, thickness);
            drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, bottom), new Vector2(right, bottom), border, thickness);
            drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, top), new Vector2(left, bottom), border, thickness);
            if (!open)
            {
                drawArgs.Context.StrokeLineSegment(origin, new Vector2(right, top), new Vector2(right, bottom), border, thickness);
            }
        }

        if (info.CanDrag)
        {
            DrawGrip(drawArgs, origin, left, top, height, baseColor, opacity);
        }

        float textRight = right;
        if (open)
        {
            // An arrow head tells that the bar goes on until the end of the cutscene.
            float arrowX = Math.Max(left + 6f, right - 10f);
            float centerY = (top + bottom) * 0.5f;
            drawArgs.Context.FillTriangle(
                origin,
                new Vector2(arrowX, centerY - 5f), border,
                new Vector2(arrowX + 7f, centerY), border,
                new Vector2(arrowX, centerY + 5f), border);
            textRight = arrowX - 2f;
        }

        float labelEnd = Math.Min(textRight, left + (float)((info.LabelEndSeconds - info.Item.StartSeconds) * context.Transform.PixelsPerSecond));
        DrawLabelInside(drawArgs, context, origin, info, item, left + LabelPadding + info.LabelInset, labelEnd - LabelPadding, top, height, opacity);
    }

    private static void DrawRange(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        Vector2 origin,
        CutsceneTimelineItemInfo info,
        TimelineItem item,
        RectangleF bounds,
        Color baseColor,
        bool selected,
        bool hovered,
        float opacity)
    {
        float top = bounds.Top + 3f;
        float bottom = bounds.Bottom - 3f;
        float left = bounds.Left;
        float right = Math.Max(bounds.Left + 3f, bounds.Right);
        float fillAlpha = (selected || hovered) ? 0.36f : 0.24f;
        drawArgs.Context.FillRectangle(origin, new RectangleF(left, top, right - left, bottom - top), baseColor * (fillAlpha * opacity));

        Color border = selected ? Color.White * opacity : baseColor * opacity;
        float thickness = selected ? 2f : 1.5f;
        drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, top), new Vector2(right, top), border, thickness);
        drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, bottom), new Vector2(right, bottom), border, thickness);
        drawArgs.Context.StrokeLineSegment(origin, new Vector2(left, top), new Vector2(left, bottom), border, thickness);
        drawArgs.Context.StrokeLineSegment(origin, new Vector2(right, top), new Vector2(right, bottom), border, thickness);

        if (info.CanDrag)
        {
            DrawGrip(drawArgs, origin, left, top, bottom - top, baseColor, opacity);
        }

        DrawLabelInside(drawArgs, context, origin, info, item, left + LabelPadding, right - LabelPadding, top, bottom - top, opacity);
    }

    private static void DrawMarker(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        Vector2 origin,
        CutsceneTimelineItemInfo info,
        TimelineItem item,
        RectangleF bounds,
        Color baseColor,
        bool selected,
        bool hovered,
        float opacity)
    {
        float x = bounds.Left;
        float centerY = bounds.Center.Y;
        float half = selected || hovered ? MarkerHalfSize + 1f : MarkerHalfSize;
        Vector2 topPoint = new(x, centerY - half);
        Vector2 rightPoint = new(x + half, centerY);
        Vector2 bottomPoint = new(x, centerY + half);
        Vector2 leftPoint = new(x - half, centerY);

        Color fill = baseColor * opacity;
        drawArgs.Context.FillTriangle(origin, topPoint, fill, rightPoint, fill, bottomPoint, fill);
        drawArgs.Context.FillTriangle(origin, topPoint, fill, bottomPoint, fill, leftPoint, fill);

        Color border = selected ? Color.White * opacity : Color.Lerp(baseColor, Color.Black, 0.45f) * opacity;
        float thickness = selected ? 2f : 1f;
        drawArgs.Context.StrokeLineSegment(origin, topPoint, rightPoint, border, thickness);
        drawArgs.Context.StrokeLineSegment(origin, rightPoint, bottomPoint, border, thickness);
        drawArgs.Context.StrokeLineSegment(origin, bottomPoint, leftPoint, border, thickness);
        drawArgs.Context.StrokeLineSegment(origin, leftPoint, topPoint, border, thickness);

        if (info.CanDrag)
        {
            // The left half of the outline, lighter than the rest: the same sign as the left edge of a bar.
            Color grip = GetGripColor(baseColor) * opacity;
            drawArgs.Context.StrokeLineSegment(origin, bottomPoint, leftPoint, grip, GripWidth);
            drawArgs.Context.StrokeLineSegment(origin, leftPoint, topPoint, grip, GripWidth);
        }

        float room = float.MaxValue;
        if (!double.IsPositiveInfinity(info.NextStartSeconds))
        {
            room = (float)((info.NextStartSeconds - info.Item.StartSeconds) * context.Transform.PixelsPerSecond);
        }

        float textLeft = x + half + 4f;
        DrawLabel(drawArgs, context, origin, info, item, textLeft, textLeft + room - half - 8f, bounds.Top, bounds.Height, opacity, true);
    }

    private static void DrawLabelInside(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        Vector2 origin,
        CutsceneTimelineItemInfo info,
        TimelineItem item,
        float left,
        float right,
        float top,
        float height,
        float opacity)
    {
        DrawLabel(drawArgs, context, origin, info, item, left, right, top, height, opacity, false);
    }

    /// <summary>Draws the full label if it fits in [left, right], else the short one, else nothing.</summary>
    private static void DrawLabel(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        Vector2 origin,
        CutsceneTimelineItemInfo info,
        TimelineItem item,
        float left,
        float right,
        float top,
        float height,
        float opacity,
        bool onBackground)
    {
        ResolvedFont font = context.Font;
        ITextMeasurementEngine? engine = context.TextEngine;
        if (engine == null || !font.IsAvailable)
        {
            return;
        }

        float available = right - left;
        if (available <= 0f)
        {
            return;
        }

        if (info.LabelWidth < 0f)
        {
            info.LabelWidth = string.IsNullOrEmpty(item.DisplayName) ? 0f : engine.MeasureText(font, item.DisplayName).X;
            info.ShortLabelWidth = string.IsNullOrEmpty(info.ShortLabel) ? 0f : engine.MeasureText(font, info.ShortLabel).X;
        }

        string? text = null;
        if (info.LabelWidth > 0f && info.LabelWidth <= available)
        {
            text = item.DisplayName;
        }
        else if (info.ShortLabelWidth > 0f && info.ShortLabelWidth <= available)
        {
            text = info.ShortLabel;
        }
        else if (!onBackground && info.ShortLabelWidth > 0f && !string.IsNullOrEmpty(info.ShortLabel))
        {
            // A bar too short for its type name keeps the start of it, cut by the average width of a character.
            int keep = (int)(available / (info.ShortLabelWidth / info.ShortLabel.Length)) - 1;
            if (keep >= 3)
            {
                if (info.TruncatedLength != keep)
                {
                    info.TruncatedLength = keep;
                    info.TruncatedLabel = string.Concat(info.ShortLabel.AsSpan(0, Math.Min(keep, info.ShortLabel.Length)), "..");
                }

                text = info.TruncatedLabel;
            }
        }

        if (text == null)
        {
            return;
        }

        float textHeight = Math.Max(font.LineHeight, 1f);
        float y = top + Math.Max(0f, (height - textHeight) * 0.5f);
        Vector2 position = new Vector2(left, y) + (font.DrawOrigin * context.FontScale) + origin;
        Color color = onBackground ? Color.White * (0.9f * opacity) : Color.White * opacity;
        drawArgs.DT.DrawTextViaEngine(font, text, position, color, font.DrawOrigin, context.FontScale);
    }

    /// <summary>The left edge of an item that can be dragged in time (P8): a lighter strip inside the bar, so that it shows when the item is selected too.</summary>
    private static void DrawGrip(ElementDrawArgs drawArgs, Vector2 origin, float left, float top, float height, Color baseColor, float opacity)
    {
        drawArgs.Context.FillRectangle(origin, new RectangleF(left, top, GripWidth, height), GetGripColor(baseColor) * opacity);
    }

    private static Color GetGripColor(Color baseColor)
    {
        return Color.Lerp(baseColor, Color.White, 0.65f);
    }

    private static void DrawDashedLine(ElementDrawArgs drawArgs, Vector2 origin, float left, float right, float y, Color color, float thickness)
    {
        for (float x = left; x < right; x += DashLength + DashGap)
        {
            drawArgs.Context.StrokeLineSegment(origin, new Vector2(x, y), new Vector2(Math.Min(x + DashLength, right), y), color, thickness);
        }
    }
}
