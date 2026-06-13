#nullable enable

using System;
using System.Globalization;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineViewport : MGElement
{
    private readonly TimelineControl _owner;
    private readonly MGToolTip _itemToolTip;
    private TimelineItem? _hoveredItem;
    private TimelineItem? _pressedItem;
    private TimelineItem? _draggedItem;
    private float _draggedItemTimeSeconds;
    private bool _ignoreNextRelease;

    public TimelineViewport(MGWindow window, TimelineControl owner)
        : base(window, MGElementType.Misc)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _itemToolTip = new MGToolTip(window, this, 0, 0)
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
        int desiredHeight = (TimelineControlMetrics.ViewportVerticalPadding * 2) + (_owner.GetTrackCount() * TimelineControlMetrics.TrackRowHeight);
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
        DrawItems(DA, origin, layoutBounds, timeAreaBounds);
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
            Rectangle laneBounds = _owner.GetTrackBounds(layoutBounds, laneIndex);
            bool isSelected = _owner.ViewState.SelectedTrackId == lane.Id;
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

    private void DrawItems(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle timeAreaBounds)
    {
        if (_owner.Model == null)
        {
            return;
        }

        float itemHalfSize = Math.Min(TimelineControlMetrics.ItemHalfSize, Math.Max(3f, (timeAreaBounds.Height * 0.5f) - 1f));
        TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float fontScale);

        for (var index = 0; index < _owner.Model.Items.Count; index++)
        {
            TimelineItem item = _owner.Model.Items[index];
            int trackIndex = _owner.GetTrackIndex(item.TrackId);
            if (trackIndex < 0)
            {
                continue;
            }

            Rectangle trackBounds = _owner.GetTrackBounds(layoutBounds, trackIndex);
            float centerY = trackBounds.Center.Y;
            float renderedStart = GetRenderedItemTime(item);
            float startX = timeAreaBounds.Left + _owner.ViewTransform.TimeToViewportX(renderedStart);

            bool isSelected = _owner.ViewState.SelectedItemId == item.Id;
            bool isHovered = _hoveredItem?.Id == item.Id;

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

            switch (item.Kind)
            {
                case TimelineItemKind.Duration:
                case TimelineItemKind.Range:
                {
                    float endX = timeAreaBounds.Left + _owner.ViewTransform.TimeToViewportX(renderedStart + Math.Max(0f, item.Duration));
                    if (endX < timeAreaBounds.Left - TimelineControlMetrics.Epsilon || startX > timeAreaBounds.Right + TimelineControlMetrics.Epsilon)
                    {
                        continue;
                    }

                    if (item.Kind == TimelineItemKind.Range)
                    {
                        DrawRangeItem(DA, origin, startX, endX, trackBounds, fillColor, strokeColor);
                    }
                    else
                    {
                        DrawDurationItem(DA, origin, startX, endX, trackBounds, fillColor, strokeColor, item, textEngine, font, fontScale, DA.Opacity);
                    }

                    break;
                }

                case TimelineItemKind.Marker:
                {
                    if (startX < timeAreaBounds.Left - itemHalfSize || startX > timeAreaBounds.Right + itemHalfSize)
                    {
                        continue;
                    }

                    DA.Context.StrokeLineSegment(origin, new Vector2(startX, layoutBounds.Top), new Vector2(startX, layoutBounds.Bottom), strokeColor, 1.5f);
                    break;
                }

                default:
                {
                    if (startX < timeAreaBounds.Left - itemHalfSize || startX > timeAreaBounds.Right + itemHalfSize)
                    {
                        continue;
                    }

                    DrawDiamond(DA, origin, startX, centerY, itemHalfSize, fillColor, strokeColor);
                    break;
                }
            }
        }
    }

    private static void DrawDiamond(ElementDrawArgs DA, Vector2 origin, float x, float centerY, float itemHalfSize, Color fillColor, Color strokeColor)
    {
        Vector2 top = new(x, centerY - itemHalfSize);
        Vector2 right = new(x + itemHalfSize, centerY);
        Vector2 bottom = new(x, centerY + itemHalfSize);
        Vector2 left = new(x - itemHalfSize, centerY);

        DA.Context.FillTriangle(origin, top, fillColor, right, fillColor, bottom, fillColor);
        DA.Context.FillTriangle(origin, top, fillColor, bottom, fillColor, left, fillColor);

        DA.Context.StrokeLineSegment(origin, top, right, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, right, bottom, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, bottom, left, strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, left, top, strokeColor, 1f);
    }

    private static void DrawBlockBorder(ElementDrawArgs DA, Vector2 origin, float left, float top, float right, float bottom, Color strokeColor)
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

    private void DrawDurationItem(ElementDrawArgs DA, Vector2 origin, float startX, float endX, Rectangle trackBounds, Color fillColor, Color strokeColor, TimelineItem item, ITextMeasurementEngine textEngine, ResolvedFont font, float fontScale, float opacity)
    {
        float top = trackBounds.Top + 3f;
        float bottom = trackBounds.Bottom - 3f;
        float left = startX;
        float right = Math.Max(startX + 2f, endX);
        float width = right - left;
        float height = Math.Max(6f, bottom - top);

        DA.Context.FillRectangle(origin, new RectangleF(left, top, width, height), fillColor * 0.55f);
        DrawBlockBorder(DA, origin, left, top, right, top + height, strokeColor);

        string label = string.IsNullOrEmpty(item.DisplayName) ? item.ItemType : item.DisplayName;
        if (string.IsNullOrEmpty(label) || !font.IsAvailable)
        {
            return;
        }

        Vector2 textSize = textEngine.MeasureText(font, label);
        if (textSize.X > width - 6f)
        {
            return;
        }

        float textHeight = Math.Max(textSize.Y, font.LineHeight * fontScale);
        float labelX = left + 4f;
        float labelY = top + Math.Max(0f, (height - textHeight) * 0.5f);
        Vector2 drawPosition = new Vector2(labelX, labelY) + (font.DrawOrigin * fontScale) + origin;
        DA.DT.DrawTextViaEngine(font, label, drawPosition, Color.Black * opacity, font.DrawOrigin, fontScale);
    }

    private void DrawRangeItem(ElementDrawArgs DA, Vector2 origin, float startX, float endX, Rectangle trackBounds, Color fillColor, Color strokeColor)
    {
        float left = startX;
        float right = Math.Max(startX + 2f, endX);
        DA.Context.FillRectangle(origin, new RectangleF(left, trackBounds.Top + 2f, right - left, Math.Max(4f, trackBounds.Height - 4f)), fillColor * 0.25f);
        DA.Context.StrokeLineSegment(origin, new Vector2(left, trackBounds.Top), new Vector2(left, trackBounds.Bottom), strokeColor, 1f);
        DA.Context.StrokeLineSegment(origin, new Vector2(right, trackBounds.Top), new Vector2(right, trackBounds.Bottom), strokeColor, 1f);
    }

    private bool TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale)
    {
        textEngine = GetTextEngine();
        font = textEngine.ResolveFont(new FontSpec(ParentWindow.Desktop.DefaultFontFamily, TimelineControlMetrics.LabelFontSize, CustomFontStyles.Normal));
        scale = font.SuggestedScale;
        return font.IsAvailable;
    }

    private void OnLeftMouseReleasedInside(object? sender, BaseMouseReleasedEventArgs e)
    {
        if (_owner.Model == null)
        {
            return;
        }

        if (_draggedItem != null)
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

        TimelineTrack? lane = _owner.GetTrackAtY(layoutBounds, layoutPosition.Y);
        if (lane != null)
        {
            _owner.SetSelectedTrackId(lane.Id, true);
        }

        TimelineItem? hitEvent = lane != null ? HitTestItem(layoutPosition, layoutBounds, timeAreaBounds, lane) : null;
        _owner.SetSelectedItemId(hitEvent?.Id, true);

        if (hitEvent == null)
        {
            float timeSeconds = GetTimeAtPosition(layoutPosition.X, timeAreaBounds.Left);
            _owner.SetCurrentTimeSeconds(timeSeconds, notify: false);
            _owner.NotifyTimeScrubbed(timeSeconds);
        }

        _pressedItem = null;
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

        TimelineTrack? lane = _owner.GetTrackAtY(layoutBounds, layoutPosition.Y);
        if (lane != null)
        {
            _owner.SetSelectedTrackId(lane.Id, true);
        }

        _pressedItem = lane != null ? HitTestItem(layoutPosition, layoutBounds, timeAreaBounds, lane) : null;
        if (_pressedItem != null)
        {
            _owner.SetSelectedItemId(_pressedItem.Id, true);
        }
        else
        {
            _owner.SetSelectedItemId(null, true);
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

        TimelineTrack? lane = _owner.GetTrackAtY(layoutBounds, layoutPosition.Y);
        if (lane == null)
        {
            return;
        }

        TimelineItem? hitEvent = HitTestItem(layoutPosition, layoutBounds, timeAreaBounds, lane);
        _owner.SetSelectedTrackId(lane.Id, true);
        _owner.SetSelectedItemId(hitEvent?.Id, true);

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

        if (_pressedItem == null || !_pressedItem.IsEditable)
        {
            return;
        }

        _draggedItem = _pressedItem;
        _draggedItemTimeSeconds = _pressedItem.StartTime;
        _owner.Focus(KeyboardFocusSource.Pointer);
        e.SetHandledBy(this, false);
    }

    private void OnDragged(object? sender, BaseMouseDraggedEventArgs e)
    {
        if (_draggedItem == null || !e.IsLMB)
        {
            return;
        }

        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        float x = Math.Max(layoutPosition.X, timeAreaBounds.Left);
        _draggedItemTimeSeconds = GetTimeAtPosition(x, timeAreaBounds.Left);
        _owner.InvalidateViewPresentation();
    }

    private void OnDragEnd(object? sender, BaseMouseDragEndEventArgs e)
    {
        if (_draggedItem == null || !e.IsLMB)
        {
            return;
        }

        bool controlDown = _owner.KeyboardHandler.Tracker.IsControlDown;
        if (controlDown)
        {
            _owner.DuplicateDraggedItem(_draggedItem.Id, _draggedItemTimeSeconds);
        }
        else
        {
            _owner.CommitDraggedItemTime(_draggedItem.Id, _draggedItemTimeSeconds);
        }

        _draggedItem = null;
        _pressedItem = null;
        _ignoreNextRelease = true;
        _owner.InvalidateViewPresentation();
    }

    private void OnMouseMovedInside(object? sender, BaseMouseMovedEventArgs e)
    {
        if (_owner.Model == null || _draggedItem != null)
        {
            return;
        }

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.CurrentPosition);
        Rectangle layoutBounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        Rectangle timeAreaBounds = GetTimeAreaBounds(layoutBounds);
        TimelineTrack? lane = timeAreaBounds.Contains(layoutPosition)
            ? _owner.GetTrackAtY(layoutBounds, layoutPosition.Y)
            : null;
        TimelineItem? hitEvent = lane != null
            ? HitTestItem(layoutPosition, layoutBounds, timeAreaBounds, lane)
            : null;

        if (ReferenceEquals(hitEvent, _hoveredItem) || (hitEvent != null && _hoveredItem?.Id == hitEvent.Id))
        {
            return;
        }

        _hoveredItem = hitEvent;
        RefreshToolTip();
        _owner.InvalidateViewPresentation();
    }

    private void OnMouseExited(object? sender, BaseMouseMovedEventArgs e)
    {
        if (_hoveredItem == null || _draggedItem != null)
        {
            return;
        }

        _hoveredItem = null;
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

    private TimelineItem? HitTestItem(Point layoutPosition, Rectangle layoutBounds, Rectangle timeAreaBounds, TimelineTrack lane)
    {
        if (_owner.Model == null)
        {
            return null;
        }

        int laneIndex = _owner.GetTrackIndex(lane.Id);
        if (laneIndex < 0)
        {
            return null;
        }

        return TimelineHitTest.HitTestNearestItem(
            _owner.Model.Items,
            _owner.ViewTransform,
            timeAreaBounds.Left,
            lane.Id,
            _owner.GetTrackBounds(layoutBounds, laneIndex).Center.Y,
            TimelineControlMetrics.ItemHitRadius,
            layoutPosition);
    }

    private void RefreshToolTip()
    {
        if (_hoveredItem == null)
        {
            ToolTip = null;
            return;
        }

        string toolTipText = string.IsNullOrWhiteSpace(_hoveredItem.ToolTipText)
            ? BuildDefaultToolTipText(_hoveredItem)
            : _hoveredItem.ToolTipText;
        _itemToolTip.SetContent(toolTipText, null, 12);
        _itemToolTip.ApplySizeToContent(SizeToContent.WidthAndHeight, 40, 24, 520, 360, false);
        ToolTip = _itemToolTip;
    }

    private string BuildDefaultToolTipText(TimelineItem timelineEvent)
    {
        TimelineTrack? lane = _owner.GetTrack(timelineEvent.TrackId);
        string laneLabel = string.IsNullOrWhiteSpace(lane?.Label) ? string.Empty : $"Track: {lane.Label}\n";
        return $"{laneLabel}Type: {timelineEvent.ItemType}\nTime: {GetRenderedItemTime(timelineEvent).ToString("0.###", CultureInfo.InvariantCulture)} s";
    }

    private float GetRenderedItemTime(TimelineItem timelineEvent)
    {
        if (_draggedItem != null && _draggedItem.Id == timelineEvent.Id)
        {
            return _draggedItemTimeSeconds;
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