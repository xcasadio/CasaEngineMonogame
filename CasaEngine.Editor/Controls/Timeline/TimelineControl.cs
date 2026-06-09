#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;

namespace CasaEngine.Editor.Controls.Timeline;

internal class TimelineControl : MGGrid
{
    private readonly RowDefinition _scrollBarRow;
    private readonly ColumnDefinition _trackColumn;
    private readonly MGGrid _timelineGrid;
    private readonly TimelineCornerHeader _cornerHeader;
    private readonly TimelineRuler _ruler;
    private readonly TimelineTrackHeaderPanel _trackHeaderPanel;
    private readonly TimelineViewport _viewport;
    private readonly TimelineHorizontalScrollBar _horizontalScrollBar;
    private TimelineModel? _model;
    private float _currentTimeSeconds;
    private bool _suppressScrollBarCallback;
    private int _lastViewportWidth = -1;
    private float _lastPixelsPerSecond = float.NaN;
    private float _lastTimelineEndSeconds = float.NaN;

    public TimelineViewState ViewState { get; } = new();

    public TimelineViewTransform ViewTransform { get; } = new();

    public TimelineModel? Model => _model;

    public float CurrentTimeSeconds => _currentTimeSeconds;

    public float MinimumPixelsPerSecond { get; set; } = 48f;

    public float MaximumPixelsPerSecond { get; set; } = 288f;

    public float PixelsPerSecond
    {
        get => ViewState.PixelsPerSecond;
        set => SetPixelsPerSecond(value);
    }

    public string CornerHeaderText
    {
        get => _cornerHeader.Text;
        set => _cornerHeader.Text = value ?? string.Empty;
    }

    public string TrackHeaderText
    {
        get => _trackHeaderPanel.Text;
        set => _trackHeaderPanel.Text = value ?? string.Empty;
    }

    public event Action<float>? PixelsPerSecondChanged;

    public event Action<TimelineEvent?>? SelectedEventChanged;

    public event Action<TimelineLane?>? SelectedLaneChanged;

    public event Action<float>? TimeScrubbed;

    public event Action<TimelineEvent, float>? EventTimeEditCommitted;

    public event Action<TimelineLane, float>? InsertRequested;

    public event Action<TimelineEvent, float>? DuplicateRequested;

    public event Action<TimelineEvent>? DeleteRequested;

    public event Action<TimelineLane, string>? LaneLabelEditCommitted;

    public TimelineControl(MGWindow window)
        : base(window)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ColumnSpacing = 0;
        RowSpacing = 0;

        AddColumn(GridLength.CreateWeightedLength(1));
        AddRow(GridLength.Auto);
        _scrollBarRow = AddRow(GridLength.CreatePixelLength(0));

        _timelineGrid = new MGGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnSpacing = 1,
            RowSpacing = 1,
            GridLineMargin = 0,
            GridLinesVisibility = GridLinesVisibility.InnerHorizontal | GridLinesVisibility.InnerVertical,
            HorizontalGridLineBrush = new MGSolidFillBrush(CasaEngine.Editor.Styling.EditorThemePalette.PreviewSurfaceBorder),
            VerticalGridLineBrush = new MGSolidFillBrush(CasaEngine.Editor.Styling.EditorThemePalette.PreviewSurfaceBorder),
        };
        _trackColumn = _timelineGrid.AddColumn(GridLength.CreatePixelLength(TimelineControlMetrics.TrackColumnWidth));
        _timelineGrid.AddColumn(GridLength.CreateWeightedLength(1));
        _timelineGrid.AddRow(GridLength.Auto);
        _timelineGrid.AddRow(GridLength.Auto);

        _cornerHeader = new TimelineCornerHeader(window);
        _ruler = new TimelineRuler(window, this);
    _trackHeaderPanel = new TimelineTrackHeaderPanel(window, this);
        _trackHeaderPanel.LaneLabelEdited += (lane, label) => LaneLabelEditCommitted?.Invoke(lane, label);
        _viewport = new TimelineViewport(window, this);
        _horizontalScrollBar = new TimelineHorizontalScrollBar(window);
        _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;

        _timelineGrid.TryAddChild(0, 0, _cornerHeader);
        _timelineGrid.TryAddChild(0, 1, _ruler);
        _timelineGrid.TryAddChild(1, 0, _trackHeaderPanel);
        _timelineGrid.TryAddChild(1, 1, _viewport);

        TryAddChild(0, 0, _timelineGrid);
        TryAddChild(1, 0, _horizontalScrollBar);

        ViewState.PixelsPerSecond = 96f;
        SyncTransformFromViewState();
        UpdateHorizontalScrollBarState();

        KeyboardHandler.Pressed += OnKeyboardPressed;
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);
        UpdateTrackColumnWidth();

        int viewportWidth = (int)MathF.Round(_viewport.GetVisibleTimeAreaWidth());
        float timelineEndSeconds = GetTimelineEndSeconds();
        if (viewportWidth == _lastViewportWidth
            && Math.Abs(_lastPixelsPerSecond - ViewState.PixelsPerSecond) < TimelineControlMetrics.Epsilon
            && Math.Abs(_lastTimelineEndSeconds - timelineEndSeconds) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        _lastViewportWidth = viewportWidth;
        _lastPixelsPerSecond = ViewState.PixelsPerSecond;
        _lastTimelineEndSeconds = timelineEndSeconds;
        UpdateHorizontalScrollBarState();
    }

    public void SetModel(TimelineModel? model)
    {
        _model = model;

        if (_model == null)
        {
            _currentTimeSeconds = 0f;
            ViewState.SelectedEventId = null;
            ViewState.SelectedLaneId = null;
        }
        else
        {
            _currentTimeSeconds = Math.Clamp(_currentTimeSeconds, 0f, GetTimelineEndSeconds());
            TimelineEvent? selectedEvent = GetSelectedEvent();
            if (ViewState.SelectedEventId.HasValue && selectedEvent == null)
            {
                ViewState.SelectedEventId = null;
            }

            if (selectedEvent != null)
            {
                ViewState.SelectedLaneId = selectedEvent.LaneId;
            }
            else if (_model.Lanes.Count > 0 && (!ViewState.SelectedLaneId.HasValue || GetLane(ViewState.SelectedLaneId.Value) == null))
            {
                ViewState.SelectedLaneId = _model.Lanes[0].Id;
            }
            else if (_model.Lanes.Count == 0)
            {
                ViewState.SelectedLaneId = null;
            }
        }

        _trackHeaderPanel.RefreshRows();
        UpdateTrackColumnWidth();
        ClampScrollToViewport();
        UpdateHorizontalScrollBarState();
        InvalidateViewPresentation();
    }

    public void SetCurrentTimeSeconds(float currentTimeSeconds)
    {
        SetCurrentTimeSeconds(currentTimeSeconds, notify: false);
    }

    public void SetCurrentTimeSeconds(float currentTimeSeconds, bool notify)
    {
        float actualTime = Math.Clamp(currentTimeSeconds, 0f, GetTimelineEndSeconds());
        if (Math.Abs(_currentTimeSeconds - actualTime) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        _currentTimeSeconds = actualTime;
        InvalidateViewPresentation();
        if (notify)
        {
            NotifyTimeScrubbed(actualTime);
        }
    }

    public void SetSelectedEventId(Guid? selectedEventId)
    {
        SetSelectedEventId(selectedEventId, true);
    }

    public void SetSelectedEventId(Guid? selectedEventId, bool notify)
    {
        Guid? actualSelectedEventId = selectedEventId;
        TimelineEvent? selectedEvent = null;
        if (actualSelectedEventId.HasValue)
        {
            selectedEvent = FindEvent(actualSelectedEventId.Value);
            if (selectedEvent == null)
            {
                actualSelectedEventId = null;
            }
        }

        if (ViewState.SelectedEventId == actualSelectedEventId)
        {
            if (selectedEvent != null && ViewState.SelectedLaneId != selectedEvent.LaneId)
            {
                SetSelectedLaneId(selectedEvent.LaneId, notify);
            }

            return;
        }

        ViewState.SelectedEventId = actualSelectedEventId;
        if (selectedEvent != null)
        {
            SetSelectedLaneId(selectedEvent.LaneId, notify);
        }

        InvalidateViewPresentation();
        if (notify)
        {
            SelectedEventChanged?.Invoke(selectedEvent);
        }
    }

    public void SetSelectedLaneId(Guid? selectedLaneId)
    {
        SetSelectedLaneId(selectedLaneId, true);
    }

    public void SetSelectedLaneId(Guid? selectedLaneId, bool notify)
    {
        Guid? actualSelectedLaneId = selectedLaneId;
        if (_model == null || _model.Lanes.Count == 0)
        {
            actualSelectedLaneId = null;
        }
        else
        {
            if (actualSelectedLaneId.HasValue && GetLane(actualSelectedLaneId.Value) == null)
            {
                actualSelectedLaneId = null;
            }

            actualSelectedLaneId ??= _model.Lanes[0].Id;
        }

        if (ViewState.SelectedLaneId == actualSelectedLaneId)
        {
            return;
        }

        ViewState.SelectedLaneId = actualSelectedLaneId;
        _trackHeaderPanel.RefreshSelection();
        InvalidateViewPresentation();
        if (notify)
        {
            SelectedLaneChanged?.Invoke(GetSelectedLane());
        }
    }

    internal float GetTimelineEndSeconds()
    {
        float timelineEndSeconds = Math.Max(0f, _currentTimeSeconds);
        if (_model != null)
        {
            timelineEndSeconds = Math.Max(timelineEndSeconds, Math.Max(0f, _model.DurationSeconds));
            for (var index = 0; index < _model.Events.Count; index++)
            {
                timelineEndSeconds = Math.Max(timelineEndSeconds, _model.Events[index].TimeSeconds);
            }
        }

        return timelineEndSeconds > 0f ? timelineEndSeconds : 1f;
    }

    internal void NotifyTimeScrubbed(float timeSeconds)
    {
        TimeScrubbed?.Invoke(timeSeconds);
    }

    internal int GetLaneCount()
    {
        return _model?.Lanes.Count > 0 ? _model.Lanes.Count : 1;
    }

    internal Rectangle GetLaneBounds(Rectangle layoutBounds, int laneIndex)
    {
        Rectangle contentBounds = GetLaneContentBounds(layoutBounds);
        int actualLaneIndex = Math.Clamp(laneIndex, 0, GetLaneCount() - 1);
        int top = contentBounds.Top + (actualLaneIndex * TimelineControlMetrics.TrackRowHeight);
        return new Rectangle(layoutBounds.Left, top, Math.Max(1, layoutBounds.Width), TimelineControlMetrics.TrackRowHeight);
    }

    internal TimelineLane? GetLaneAtY(Rectangle layoutBounds, int y)
    {
        if (_model == null || _model.Lanes.Count == 0)
        {
            return null;
        }

        Rectangle contentBounds = GetLaneContentBounds(layoutBounds);
        if (y < contentBounds.Top || y >= contentBounds.Bottom)
        {
            return null;
        }

        int laneIndex = Math.Clamp((y - contentBounds.Top) / TimelineControlMetrics.TrackRowHeight, 0, _model.Lanes.Count - 1);
        return _model.Lanes[laneIndex];
    }

    internal int GetLaneIndex(Guid laneId)
    {
        if (_model == null)
        {
            return -1;
        }

        for (var index = 0; index < _model.Lanes.Count; index++)
        {
            if (_model.Lanes[index].Id == laneId)
            {
                return index;
            }
        }

        return -1;
    }

    internal TimelineLane? GetLane(Guid laneId)
    {
        if (_model == null)
        {
            return null;
        }

        for (var index = 0; index < _model.Lanes.Count; index++)
        {
            if (_model.Lanes[index].Id == laneId)
            {
                return _model.Lanes[index];
            }
        }

        return null;
    }

    internal TimelineLane? GetSelectedLane()
    {
        if (_model == null || _model.Lanes.Count == 0)
        {
            return null;
        }

        if (ViewState.SelectedLaneId.HasValue)
        {
            TimelineLane? lane = GetLane(ViewState.SelectedLaneId.Value);
            if (lane != null)
            {
                return lane;
            }
        }

        return _model.Lanes.Count > 0 ? _model.Lanes[0] : null;
    }

    internal void CommitDraggedEventTime(Guid eventId, float timeSeconds)
    {
        TimelineEvent? timelineEvent = FindEvent(eventId);
        if (timelineEvent == null || !timelineEvent.IsEditable)
        {
            return;
        }

        float actualTime = Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds());
        if (Math.Abs(actualTime - timelineEvent.TimeSeconds) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        EventTimeEditCommitted?.Invoke(timelineEvent, actualTime);
    }

    internal bool TryInsertAtSelectedLane(float timeSeconds)
    {
        TimelineLane? lane = GetSelectedLane();
        if (lane == null || !lane.IsEditable)
        {
            return false;
        }

        InsertRequested?.Invoke(lane, Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds()));
        return true;
    }

    internal bool TryDuplicateSelectedEvent(float timeSeconds)
    {
        TimelineEvent? selectedEvent = GetSelectedEvent();
        if (selectedEvent == null || !selectedEvent.IsEditable)
        {
            return false;
        }

        DuplicateRequested?.Invoke(selectedEvent, Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds()));
        return true;
    }

    internal bool TryDeleteSelectedEvent()
    {
        TimelineEvent? selectedEvent = GetSelectedEvent();
        if (selectedEvent == null || !selectedEvent.IsEditable)
        {
            return false;
        }

        DeleteRequested?.Invoke(selectedEvent);
        return true;
    }

    internal MGContextMenu? CreateContextMenu(TimelineLane? lane, TimelineEvent? timelineEvent, float cursorTimeSeconds)
    {
        if (ParentWindow == null)
        {
            return null;
        }

        MGContextMenu menu = new(ParentWindow, string.Empty);
        float actualCursorTime = Math.Clamp(cursorTimeSeconds, 0f, GetTimelineEndSeconds());

        if (lane != null && lane.IsEditable)
        {
            string laneLabel = string.IsNullOrWhiteSpace(lane.Label) ? "lane" : lane.Label;
            menu.AddButton($"Add on {laneLabel} at cursor", _ => InsertRequested?.Invoke(lane, actualCursorTime));
            menu.AddButton($"Add on {laneLabel} at playhead", _ => InsertRequested?.Invoke(lane, CurrentTimeSeconds));
        }

        if (timelineEvent != null && timelineEvent.IsEditable)
        {
            if (menu.Items.Count > 0)
            {
                menu.AddSeparator();
            }

            string itemLabel = string.IsNullOrWhiteSpace(timelineEvent.EventType) ? "item" : timelineEvent.EventType;
            menu.AddButton($"Duplicate {itemLabel} at cursor", _ => DuplicateRequested?.Invoke(timelineEvent, actualCursorTime));
            menu.AddButton($"Duplicate {itemLabel} at playhead", _ => DuplicateRequested?.Invoke(timelineEvent, CurrentTimeSeconds));
            menu.AddButton($"Delete {itemLabel}", _ => DeleteRequested?.Invoke(timelineEvent));
        }

        return menu.Items.Count > 0 ? menu : null;
    }

    internal void ApplyMouseWheelZoom(float wheelSteps, float anchorViewportX)
    {
        if (Math.Abs(wheelSteps) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        float actualAnchorViewportX = Math.Max(0f, anchorViewportX);
        float anchorTimeSeconds = ViewTransform.ViewportXToTime(actualAnchorViewportX);
        float desiredPixelsPerSecond = ViewState.PixelsPerSecond * MathF.Pow(TimelineControlMetrics.MouseWheelZoomMultiplier, wheelSteps);
        SetPixelsPerSecond(desiredPixelsPerSecond, actualAnchorViewportX, anchorTimeSeconds);
    }

    internal void InvalidateViewPresentation()
    {
        ArrangeChanged(this, true);
    }

    private void SetPixelsPerSecond(float desiredPixelsPerSecond)
    {
        SetPixelsPerSecond(desiredPixelsPerSecond, null, null);
    }

    private void SetPixelsPerSecond(float desiredPixelsPerSecond, float? anchorViewportX, float? anchorTimeSeconds)
    {
        float actualPixelsPerSecond = Math.Clamp(desiredPixelsPerSecond, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
        if (Math.Abs(ViewState.PixelsPerSecond - actualPixelsPerSecond) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        ViewState.PixelsPerSecond = actualPixelsPerSecond;
        if (anchorViewportX.HasValue && anchorTimeSeconds.HasValue)
        {
            ViewState.ScrollX = ViewTransform.GetScrollXForAnchor(anchorTimeSeconds.Value, anchorViewportX.Value, actualPixelsPerSecond);
        }

        SyncTransformFromViewState();
        ClampScrollToViewport();
        UpdateHorizontalScrollBarState();
        InvalidateViewPresentation();
        PixelsPerSecondChanged?.Invoke(actualPixelsPerSecond);
    }

    private TimelineEvent? GetSelectedEvent()
    {
        if (!ViewState.SelectedEventId.HasValue)
        {
            return null;
        }

        return FindEvent(ViewState.SelectedEventId.Value);
    }

    private bool ContainsEvent(Guid eventId)
    {
        return FindEvent(eventId) != null;
    }

    private TimelineEvent? FindEvent(Guid eventId)
    {
        if (_model == null)
        {
            return null;
        }

        for (var index = 0; index < _model.Events.Count; index++)
        {
            if (_model.Events[index].Id == eventId)
            {
                return _model.Events[index];
            }
        }

        return null;
    }

    private Rectangle GetLaneContentBounds(Rectangle layoutBounds)
    {
        int top = layoutBounds.Top + TimelineControlMetrics.ViewportVerticalPadding;
        int height = Math.Max(1, GetLaneCount() * TimelineControlMetrics.TrackRowHeight);
        return new Rectangle(layoutBounds.Left, top, Math.Max(1, layoutBounds.Width), height);
    }

    private void ClampScrollToViewport()
    {
        ViewState.ScrollX = ViewTransform.ClampScrollX(ViewState.ScrollX, GetTimelineEndSeconds(), _viewport.GetVisibleTimeAreaWidth());
        SyncTransformFromViewState();
    }

    private void SyncTransformFromViewState()
    {
        ViewTransform.PixelsPerSecond = ViewState.PixelsPerSecond;
        ViewTransform.ScrollX = ViewState.ScrollX;
    }

    private void UpdateHorizontalScrollBarState()
    {
        float viewportWidth = _viewport.GetVisibleTimeAreaWidth();
        float maxScrollX = ViewTransform.GetMaxScrollX(GetTimelineEndSeconds(), viewportWidth);
        float clampedScrollX = ViewTransform.ClampScrollX(ViewState.ScrollX, GetTimelineEndSeconds(), viewportWidth);
        bool hasHorizontalScroll = maxScrollX > TimelineControlMetrics.Epsilon;

        if (Math.Abs(ViewState.ScrollX - clampedScrollX) >= TimelineControlMetrics.Epsilon)
        {
            ViewState.ScrollX = clampedScrollX;
            SyncTransformFromViewState();
        }

        _suppressScrollBarCallback = true;
        _horizontalScrollBar.SetRange(0f, Math.Max(0f, maxScrollX));
        _horizontalScrollBar.Value = clampedScrollX;
        _horizontalScrollBar.IsEnabled = hasHorizontalScroll;
        _horizontalScrollBar.Visibility = hasHorizontalScroll ? Visibility.Visible : Visibility.Collapsed;
        _suppressScrollBarCallback = false;

        int rowHeight = hasHorizontalScroll ? TimelineControlMetrics.ScrollBarRowHeight : 0;
        if (!_scrollBarRow.Length.IsPixelLength || _scrollBarRow.Length.Pixels != rowHeight)
        {
            _scrollBarRow.Length = GridLength.CreatePixelLength(rowHeight);
        }
    }

    private void UpdateTrackColumnWidth()
    {
        float desiredWidth = _trackHeaderPanel.GetDesiredColumnWidth();
        int width = (int)MathF.Ceiling(Math.Max(TimelineControlMetrics.TrackColumnWidth, desiredWidth));
        if (_trackColumn.Length.IsPixelLength && _trackColumn.Length.Pixels == width)
        {
            return;
        }

        _trackColumn.Length = GridLength.CreatePixelLength(width);
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, EventArgs<float> e)
    {
        if (_suppressScrollBarCallback)
        {
            return;
        }

        float actualScrollX = ViewTransform.ClampScrollX(e.NewValue, GetTimelineEndSeconds(), _viewport.GetVisibleTimeAreaWidth());
        if (Math.Abs(ViewState.ScrollX - actualScrollX) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        ViewState.ScrollX = actualScrollX;
        SyncTransformFromViewState();
        InvalidateViewPresentation();
    }

    private void OnKeyboardPressed(object? sender, BaseKeyPressedEventArgs e)
    {
        if (e.IsHandled)
        {
            return;
        }

        bool controlDown = KeyboardHandler.Tracker.IsControlDown;
        if (!HandleShortcut(e.Key, controlDown))
        {
            return;
        }

        e.SetHandledBy(this, false);
    }

    private bool HandleShortcut(Keys key, bool controlDown)
    {
        if (controlDown && key == Keys.D)
        {
            return TryDuplicateSelectedEvent(CurrentTimeSeconds);
        }

        switch (key)
        {
            case Keys.Insert:
                return TryInsertAtSelectedLane(CurrentTimeSeconds);

            case Keys.Delete:
            case Keys.Back:
                return TryDeleteSelectedEvent();

            case Keys.Escape:
                if (ViewState.SelectedEventId.HasValue)
                {
                    SetSelectedEventId(null, true);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }
}