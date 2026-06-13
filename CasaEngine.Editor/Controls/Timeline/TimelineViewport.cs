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
    private TimelineItem? _hoveredEvent;
    private TimelineItem? _pressedEvent;
    private TimelineItem? _draggedEvent;
    private float _draggedEventTimeSeconds;
    private bool _ignoreNextRelease;

    public TimelineViewport(MGWindow window, TimelineControl owner)
        : base(window, MGElementType.Misc)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _eventToolTip = new MGToolTip(window, this, 0, 0)
        {
            ShowDelayOverride = TimeSpan.Zero,
        };

        MouseHandler.DragStartCondition = DragStartCondition.MouseMovedAfterPress;
        MouseHandler.LMBPressedInside += OnLeftMousePressedInside;
        MouseHandler.LMBReleasedInside += OnLeftMouseReleasedInside;
        MouseHandler.RMBReleasedInside += OnRightMouseReleasedInside;
        MouseHandler.DragStart += OnDragStart;
        MouseHandler.Dragged += OnDragged;
        MouseHandler.DragEnd += OnDragEnd;
        MouseHandler.MovedInside += OnMouseMovedInside;
        MouseHandler.Exited += OnMouseExited;
        MouseHandler.Scrolled += OnScrolled;
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        int desiredHeight = (TimelineControlMetrics.ViewportVerticalPadding * 2) + (_owner.GetLaneCount() * TimelineControlMetrics.TrackRowHeight);
        return new Thickness(availableSize.Width, desiredHeight, 0, 0);
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
        DrawLaneBackgrounds(DA, origin, layoutBounds);

        Color borderColor = EditorThemePalette.PreviewSurfaceBorder * DA.Opacity;
        Color gridColor = borderColor * 0.65f;

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

        DrawPlayhead(DA, origin, layoutBounds, timeAreaBounds);
        DrawEvents(DA, origin, layoutBounds, timeAreaBounds);
    }

    private void DrawLaneBackgrounds(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds)
    {
        if (_owner.Model == null || _owner.Model.Tracks.Count == 0)
        {
            return;
        }

        for (var laneIndex = 0; laneIndex < _owner.Model.Tracks.Count; laneIndex++)
        {
            TimelineTrack lane = _owner.Model.Tracks[laneIndex];
            Rectangle laneBounds = _owner.GetLaneBounds(layoutBounds, laneIndex);
            bool isSelected = _owner.ViewState.SelectedLaneId == lane.Id;
            Color laneColor = isSelected
                ? EditorThemePalette.AccentSelection * (0.18f * DA.Opacity)
                : (laneIndex % 2 == 0 ? Color.Transparent : Color.Black * (0.08f * DA.Opacity));

            if (laneColor.A > 0)
            {
                DA.Context.FillRectangle(origin, new RectangleF(laneBounds.X, laneBounds.Y, laneBounds.Width, laneBounds.Height), laneColor);
            }

            if (laneIndex > 0)
            {
                DA.Context.StrokeLineSegment(origin, new Vector2(laneBounds.Left, laneBounds.Top), new Vector2(laneBounds.Right, laneBounds.Top), EditorThemePalette.PreviewSurfaceBorder * DA.Opacity, 1f);
            }
        }
    }

    private void DrawPlayhead(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle timeAreaBounds)
    {
        float x = timeAreaBounds.Left + _owner.ViewTransform.TimeToViewportX(_owner.CurrentTimeSeconds);
        if (x < timeAreaBounds.Left - TimelineControlMetrics.Epsilon || x > timeAreaBounds.Right + TimelineControlMetrics.Epsilon)
        {
            return;
        }

        Color playheadColor = EditorThemePalette.InlineRenameBorder * DA.Opacity;
        DA.Context.StrokeLineSegment(origin, new Vector2(x, layoutBounds.Top), new Vector2(x, layoutBounds.Bottom), playheadColor, 1.5f);
    }

    private void DrawEvents(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle timeAreaBounds)
    {
        if (_owner.Model == null)
        {
            return;
        }

        float eventHalfSize = Math.Min(TimelineControlMetrics.EventHalfSize, Math.Max(3f, (timeAreaBounds.Height * 0.5f) - 1f));
        for (var index = 0; index < _owner.Model.Items.Count; index++)
        {
            TimelineItem timelineEvent = _owner.Model.Items[index];
            int laneIndex = _owner.GetLaneIndex(timelineEvent.TrackId);
            if (laneIndex < 0)
            {
                continue;
            }

            Rectangle laneBounds = _owner.GetLaneBounds(layoutBounds, laneIndex);
            float centerY = laneBounds.Center.Y;
            float renderedTime = GetRenderedEventTime(timelineEvent);
            float x = timeAreaBounds.Left + _owner.ViewTransform.TimeToViewportX(renderedTime);
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

        if (_draggedEvent != null)
        {
            e.SetHandledBy(this, false);
            return;
        }

        if (_ignoreNextRelease)
        {
            _ignoreNextRelease = false;
            return;
        }

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        if (!timeAreaBounds.Contains(layoutPosition))
        {
            return;
        }

        TimelineTrack? lane = _owner.GetLaneAtY(layoutBounds, layoutPosition.Y);
        if (lane != null)
        {
            _owner.SetSelectedLaneId(lane.Id, true);
        }

        TimelineItem? hitEvent = lane != null ? HitTestEvent(layoutPosition, layoutBounds, timeAreaBounds, lane) : null;
        _owner.SetSelectedEventId(hitEvent?.Id, true);

        if (hitEvent == null)
        {
            float timeSeconds = GetTimeAtPosition(layoutPosition.X, timeAreaBounds.Left);
            _owner.SetCurrentTimeSeconds(timeSeconds, notify: false);
            _owner.NotifyTimeScrubbed(timeSeconds);
        }

        _pressedEvent = null;
        e.SetHandledBy(this, false);
    }

    private void OnLeftMousePressedInside(object? sender, BaseMousePressedEventArgs e)
    {
        if (_owner.Model == null)
        {
            return;
        }

        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        if (!timeAreaBounds.Contains(layoutPosition))
        {
            return;
        }

        _owner.Focus(KeyboardFocusSource.Pointer);

        TimelineTrack? lane = _owner.GetLaneAtY(layoutBounds, layoutPosition.Y);
        if (lane != null)
        {
            _owner.SetSelectedLaneId(lane.Id, true);
        }

        _pressedEvent = lane != null ? HitTestEvent(layoutPosition, layoutBounds, timeAreaBounds, lane) : null;
        if (_pressedEvent != null)
        {
            _owner.SetSelectedEventId(_pressedEvent.Id, true);
        }
        else
        {
            _owner.SetSelectedEventId(null, true);
        }

        e.SetHandledBy(this, false);
    }

    private void OnRightMouseReleasedInside(object? sender, BaseMouseReleasedEventArgs e)
    {
        if (_owner.Model == null)
        {
            return;
        }

        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        if (!timeAreaBounds.Contains(layoutPosition))
        {
            return;
        }

        _owner.Focus(KeyboardFocusSource.Pointer);

        TimelineTrack? lane = _owner.GetLaneAtY(layoutBounds, layoutPosition.Y);
        if (lane == null)
        {
            return;
        }

        TimelineItem? hitEvent = HitTestEvent(layoutPosition, layoutBounds, timeAreaBounds, lane);
        _owner.SetSelectedLaneId(lane.Id, true);
        _owner.SetSelectedEventId(hitEvent?.Id, true);

        float timeSeconds = GetTimeAtPosition(layoutPosition.X, timeAreaBounds.Left);
        MGContextMenu? menu = _owner.CreateContextMenu(lane, hitEvent, timeSeconds);
        if (menu != null && menu.TryOpenContextMenu(e.Position))
        {
            e.SetHandledBy(menu, false);
        }
    }

    private void OnDragStart(object? sender, BaseMouseDragStartEventArgs e)
    {
        if (_owner.Model == null || !e.IsLMB || e.Condition != DragStartCondition.MouseMovedAfterPress)
        {
            return;
        }

        if (_pressedEvent == null || !_pressedEvent.IsEditable)
        {
            return;
        }

        _draggedEvent = _pressedEvent;
        _draggedEventTimeSeconds = _pressedEvent.StartTime;
        _owner.Focus(KeyboardFocusSource.Pointer);
        e.SetHandledBy(this, false);
    }

    private void OnDragged(object? sender, BaseMouseDraggedEventArgs e)
    {
        if (_draggedEvent == null || !e.IsLMB)
        {
            return;
        }

        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        float x = Math.Max(layoutPosition.X, timeAreaBounds.Left);
        _draggedEventTimeSeconds = GetTimeAtPosition(x, timeAreaBounds.Left);
        _owner.InvalidateViewPresentation();
    }

    private void OnDragEnd(object? sender, BaseMouseDragEndEventArgs e)
    {
        if (_draggedEvent == null || !e.IsLMB)
        {
            return;
        }

        bool controlDown = _owner.KeyboardHandler.Tracker.IsControlDown;
        if (controlDown)
        {
            _owner.DuplicateDraggedEvent(_draggedEvent.Id, _draggedEventTimeSeconds);
        }
        else
        {
            _owner.CommitDraggedEventTime(_draggedEvent.Id, _draggedEventTimeSeconds);
        }

        _draggedEvent = null;
        _pressedEvent = null;
        _ignoreNextRelease = true;
        _owner.InvalidateViewPresentation();
    }

    private void OnMouseMovedInside(object? sender, BaseMouseMovedEventArgs e)
    {
        if (_owner.Model == null || _draggedEvent != null)
        {
            return;
        }

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.CurrentPosition);
        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        TimelineTrack? lane = timeAreaBounds.Contains(layoutPosition)
            ? _owner.GetLaneAtY(layoutBounds, layoutPosition.Y)
            : null;
        TimelineItem? hitEvent = lane != null
            ? HitTestEvent(layoutPosition, layoutBounds, timeAreaBounds, lane)
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
        if (_hoveredEvent == null || _draggedEvent != null)
        {
            return;
        }

        _hoveredEvent = null;
        RefreshToolTip();
        _owner.InvalidateViewPresentation();
    }

    private void OnScrolled(object? sender, BaseMouseScrolledEventArgs e)
    {
        if (_owner.Model == null || e.ScrollWheelDelta == 0)
        {
            return;
        }

        Rectangle bounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        if (!bounds.Contains(e.Position))
        {
            return;
        }

        int timeAreaLeft = bounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft;
        int timeAreaRight = bounds.Right - TimelineControlMetrics.TimeAreaPaddingRight;
        if (e.Position.X < timeAreaLeft || e.Position.X > timeAreaRight)
        {
            return;
        }

        float wheelSteps = e.ScrollWheelDelta / 120.0f;
        float anchorViewportX = Math.Clamp(e.Position.X - timeAreaLeft, 0f, GetVisibleTimeAreaWidth());
        _owner.ApplyMouseWheelZoom(wheelSteps, anchorViewportX);
        e.SetHandledBy(this);
    }

    private TimelineItem? HitTestEvent(Point layoutPosition, Rectangle layoutBounds, Rectangle timeAreaBounds, TimelineTrack lane)
    {
        if (_owner.Model == null)
        {
            return null;
        }

        int laneIndex = _owner.GetLaneIndex(lane.Id);
        if (laneIndex < 0)
        {
            return null;
        }

        return TimelineHitTest.HitTestNearestEvent(
            _owner.Model.Items,
            _owner.ViewTransform,
            timeAreaBounds.Left,
            lane.Id,
            _owner.GetLaneBounds(layoutBounds, laneIndex).Center.Y,
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

        string toolTipText = string.IsNullOrWhiteSpace(_hoveredEvent.ToolTipText)
            ? BuildDefaultToolTipText(_hoveredEvent)
            : _hoveredEvent.ToolTipText;
        _eventToolTip.SetContent(toolTipText, null, 12);
        _eventToolTip.ApplySizeToContent(SizeToContent.WidthAndHeight, 40, 24, 520, 360, false);
        ToolTip = _eventToolTip;
    }

    private string BuildDefaultToolTipText(TimelineItem timelineEvent)
    {
        TimelineTrack? lane = _owner.GetLane(timelineEvent.TrackId);
        string laneLabel = string.IsNullOrWhiteSpace(lane?.Label) ? string.Empty : $"Track: {lane.Label}\n";
        return $"{laneLabel}Type: {timelineEvent.ItemType}\nTime: {GetRenderedEventTime(timelineEvent).ToString("0.###", CultureInfo.InvariantCulture)} s";
    }

    private float GetRenderedEventTime(TimelineItem timelineEvent)
    {
        if (_draggedEvent != null && _draggedEvent.Id == timelineEvent.Id)
        {
            return _draggedEventTimeSeconds;
        }

        return timelineEvent.StartTime;
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
        return new Rectangle(
            layoutBounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft,
            layoutBounds.Top,
            width,
            Math.Max(1, layoutBounds.Height));
    }
}