#nullable enable

using System;
using System.Globalization;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineViewport : MGElement
{
    private readonly TimelineControl _owner;
    private readonly MGToolTip _eventToolTip;
    private TimelineEvent? _hoveredEvent;

    public TimelineViewport(MGWindow window, TimelineControl owner)
        : base(window, MGElementType.Misc)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _eventToolTip = new MGToolTip(window, this, 220, 56)
        {
            ShowDelayOverride = TimeSpan.Zero,
        };

        MouseHandler.LMBReleasedInside += OnLeftMouseReleasedInside;
        MouseHandler.MovedInside += OnMouseMovedInside;
        MouseHandler.Exited += OnMouseExited;
    }

    public float GetVisibleTimeAreaWidth()
    {
        return Math.Max(0f, LayoutBounds.Width - TimelineControlMetrics.TimeAreaPaddingLeft - TimelineControlMetrics.TimeAreaPaddingRight);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        DA.Context.FillRectangle(origin, new RectangleF(layoutBounds.X, layoutBounds.Y, layoutBounds.Width, layoutBounds.Height), EditorThemePalette.PreviewSurfaceBackground * DA.Opacity);

        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        Color borderColor = EditorThemePalette.PreviewSurfaceBorder * DA.Opacity;
        Color gridColor = borderColor * 0.65f;
        Color axisColor = EditorThemePalette.PanelBorder * DA.Opacity;
        Color laneFillColor = Color.Black * (0.18f * DA.Opacity);

        DA.Context.StrokeAndFillRectangle(origin, new RectangleF(timeAreaBounds.X, timeAreaBounds.Y, timeAreaBounds.Width, timeAreaBounds.Height), borderColor, laneFillColor, new Thickness(1));

        float timelineEndSeconds = _owner.GetTimelineEndSeconds();
        float majorTickStep = TimelineTickCalculator.GetMajorTickStepSeconds(_owner.ViewTransform.PixelsPerSecond, TimelineControlMetrics.MajorTickTargetPixels);
        float minorTickStep = TimelineTickCalculator.GetMinorTickStepSeconds(majorTickStep);
        float contentLeft = timeAreaBounds.Left;
        float contentRight = timeAreaBounds.Right;
        int startIndex = (int)MathF.Floor(Math.Max(0f, _owner.ViewTransform.ViewportXToTime(-TimelineControlMetrics.TimeAreaPaddingLeft)) / minorTickStep);
        int endIndex = (int)MathF.Ceiling(Math.Max(0f, _owner.ViewTransform.ViewportXToTime(timeAreaBounds.Width + TimelineControlMetrics.TimeAreaPaddingRight)) / minorTickStep) + 1;
        for (var index = startIndex; index <= endIndex; index++)
        {
            float timeSeconds = index * minorTickStep;
            if (timeSeconds < 0f)
            {
                continue;
            }

            if (timeSeconds > timelineEndSeconds + TimelineControlMetrics.Epsilon)
            {
                break;
            }

            float x = contentLeft + _owner.ViewTransform.TimeToViewportX(timeSeconds);
            if (x < contentLeft - TimelineControlMetrics.Epsilon || x > contentRight + TimelineControlMetrics.Epsilon)
            {
                continue;
            }

            bool isMajor = IsMajorTick(timeSeconds, majorTickStep);
            DA.Context.StrokeLineSegment(origin, new Vector2(x, timeAreaBounds.Top), new Vector2(x, timeAreaBounds.Bottom), isMajor ? gridColor : gridColor * 0.5f, 1f);
        }

        float centerY = timeAreaBounds.Center.Y;
        DA.Context.StrokeLineSegment(origin, new Vector2(timeAreaBounds.Left, centerY), new Vector2(timeAreaBounds.Right, centerY), axisColor, 1f);
        DrawPlayhead(DA, origin, timeAreaBounds);
        DrawEvents(DA, origin, timeAreaBounds, centerY);
    }

    private void DrawPlayhead(ElementDrawArgs DA, Vector2 origin, Rectangle timeAreaBounds)
    {
        float x = timeAreaBounds.Left + _owner.ViewTransform.TimeToViewportX(_owner.CurrentTimeSeconds);
        Color playheadColor = EditorThemePalette.InlineRenameBorder * DA.Opacity;
        DA.Context.StrokeLineSegment(origin, new Vector2(x, timeAreaBounds.Top), new Vector2(x, timeAreaBounds.Bottom), playheadColor, 1.5f);
    }

    private void DrawEvents(ElementDrawArgs DA, Vector2 origin, Rectangle timeAreaBounds, float centerY)
    {
        if (_owner.Model == null)
        {
            return;
        }

        float eventHalfSize = Math.Min(TimelineControlMetrics.EventHalfSize, Math.Max(3f, (timeAreaBounds.Height * 0.5f) - 1f));
        for (var index = 0; index < _owner.Model.Events.Count; index++)
        {
            TimelineEvent timelineEvent = _owner.Model.Events[index];
            float x = timeAreaBounds.Left + _owner.ViewTransform.TimeToViewportX(timelineEvent.TimeSeconds);
            if (x < timeAreaBounds.Left - eventHalfSize || x > timeAreaBounds.Right + eventHalfSize)
            {
                continue;
            }

            bool isSelected = _owner.ViewState.SelectedEventId == timelineEvent.Id;
            bool isHovered = _hoveredEvent?.Id == timelineEvent.Id;

            Color fillColor = isSelected
                ? EditorThemePalette.AccentSelection * DA.Opacity
                : isHovered
                    ? Color.White * DA.Opacity
                    : Color.Gainsboro * (0.9f * DA.Opacity);
            Color strokeColor = isSelected
                ? EditorThemePalette.InlineRenameBorder * DA.Opacity
                : isHovered
                    ? EditorThemePalette.AccentSelection * DA.Opacity
                    : EditorThemePalette.PanelBorder * DA.Opacity;

            DrawDiamond(DA, origin, x, centerY, eventHalfSize, fillColor, strokeColor);
        }
    }

    private static void DrawDiamond(ElementDrawArgs DA, Vector2 origin, float x, float centerY, float eventHalfSize, Color fillColor, Color strokeColor)
    {
        Vector2 top = new(x, centerY - eventHalfSize);
        Vector2 right = new(x + eventHalfSize, centerY);
        Vector2 bottom = new(x, centerY + eventHalfSize);
        Vector2 left = new(x - eventHalfSize, centerY);

        DA.Context.FillTriangle(origin, top, fillColor, right, fillColor, bottom, fillColor);
        DA.Context.FillTriangle(origin, top, fillColor, bottom, fillColor, left, fillColor);

        DA.Context.StrokeLineSegment(origin, top, right, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, right, bottom, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, bottom, left, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, left, top, strokeColor, 1f);
    }

    private void OnLeftMouseReleasedInside(object? sender, BaseMouseReleasedEventArgs e)
    {
        if (_owner.Model == null)
        {
            return;
        }

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle timeAreaBounds = GetTimeAreaBounds(LayoutBounds);
        if (!timeAreaBounds.Contains(layoutPosition))
        {
            return;
        }

        TimelineEvent? hitEvent = HitTestEvent(layoutPosition, timeAreaBounds);
        _owner.SetSelectedEventId(hitEvent?.Id, true);

        float timeSeconds = GetTimeAtPosition(layoutPosition.X, timeAreaBounds.Left);
        _owner.SetCurrentTimeSeconds(timeSeconds, notify: false);
        _owner.NotifyTimeScrubbed(timeSeconds);
        e.SetHandledBy(this, false);
    }

    private void OnMouseMovedInside(object? sender, BaseMouseMovedEventArgs e)
    {
        if (_owner.Model == null)
        {
            return;
        }

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.CurrentPosition);
        Rectangle timeAreaBounds = GetTimeAreaBounds(LayoutBounds);
        TimelineEvent? hitEvent = timeAreaBounds.Contains(layoutPosition)
            ? HitTestEvent(layoutPosition, timeAreaBounds)
            : null;

        if (ReferenceEquals(hitEvent, _hoveredEvent) || (hitEvent != null && _hoveredEvent?.Id == hitEvent.Id))
        {
            return;
        }

        _hoveredEvent = hitEvent;
        RefreshToolTip();
        _owner.InvalidateViewPresentation();
    }

    private void OnMouseExited(object? sender, BaseMouseMovedEventArgs e)
    {
        if (_hoveredEvent == null)
        {
            return;
        }

        _hoveredEvent = null;
        RefreshToolTip();
        _owner.InvalidateViewPresentation();
    }

    private TimelineEvent? HitTestEvent(Point layoutPosition, Rectangle timeAreaBounds)
    {
        if (_owner.Model == null)
        {
            return null;
        }

        return TimelineHitTest.HitTestNearestEvent(
            _owner.Model.Events,
            _owner.ViewTransform,
            timeAreaBounds.Left,
            timeAreaBounds.Center.Y,
            TimelineControlMetrics.EventHitRadius,
            layoutPosition);
    }

    private void RefreshToolTip()
    {
        if (_hoveredEvent == null)
        {
            ToolTip = null;
            return;
        }

        _eventToolTip.SetContent(
            $"Type: {_hoveredEvent.EventType}\nTime: {_hoveredEvent.TimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s",
            null,
            12);
        ToolTip = _eventToolTip;
    }

    private float GetTimeAtPosition(float x, float contentLeft)
    {
        return _owner.ViewTransform.ViewportXToTime(x - contentLeft);
    }

    private static bool IsMajorTick(float timeSeconds, float majorTickStep)
    {
        if (majorTickStep <= 0f)
        {
            return false;
        }

        float ratio = timeSeconds / majorTickStep;
        return Math.Abs(ratio - MathF.Round(ratio)) < 0.01f;
    }

    private static Rectangle GetTimeAreaBounds(Rectangle layoutBounds)
    {
        int width = Math.Max(1, layoutBounds.Width - TimelineControlMetrics.TimeAreaPaddingLeft - TimelineControlMetrics.TimeAreaPaddingRight);
        int height = Math.Max(1, layoutBounds.Height - (TimelineControlMetrics.ViewportVerticalPadding * 2));
        return new Rectangle(
            layoutBounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft,
            layoutBounds.Top + TimelineControlMetrics.ViewportVerticalPadding,
            width,
            height);
    }
}