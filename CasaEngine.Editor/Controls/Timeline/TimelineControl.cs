#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;
using CasaEngine.Editor.Controls.Timeline.Editing;
using CasaEngine.Editor.Controls.Timeline.Rendering;

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

    public ITimelineEditPolicy? EditPolicy { get; set; }

    public ITimelineAdapter? Adapter { get; set; }

    public ITimelineItemRenderer ItemRenderer { get; set; } = new DefaultTimelineItemRenderer();

    public TimelineSnapSettings SnapSettings { get; } = new();

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

    public event Action<TimelineItem?>? SelectedItemChanged;

    public event Action<TimelineTrack?>? SelectedTrackChanged;

    public event Action<float>? TimeScrubbed;

    public event Action<TimelineItem, float>? ItemTimeEditCommitted;

    public event Action<TimelineItem, float, float>? ItemResizeCommitted;

    public event Action<TimelineTrack, float>? InsertRequested;

    public event Action<TimelineItem, float>? DuplicateRequested;

    public event Action<TimelineItem>? DeleteRequested;

    public event Action<TimelineItem>? CopyRequested;

    public event Action<TimelineTrack, float>? PasteRequested;

    public event Action<TimelineTrack, string>? TrackRenameRequested;

    public event Action<TimelineTrack, string>? TrackLabelEditCommitted;

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
        _trackHeaderPanel.TrackLabelEdited += (lane, label) =>
        {
            if (Adapter != null)
            {
                Adapter.RenameTrack(lane.Id, label);
            }
            else
            {
                TrackLabelEditCommitted?.Invoke(lane, label);
            }
        };
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
            ViewState.SelectedItemId = null;
            ViewState.SelectedTrackId = null;
        }
        else
        {
            _currentTimeSeconds = Math.Clamp(_currentTimeSeconds, 0f, GetTimelineEndSeconds());
            TimelineItem? selectedEvent = GetSelectedItem();
            if (ViewState.SelectedItemId.HasValue && selectedEvent == null)
            {
                ViewState.SelectedItemId = null;
            }

            if (selectedEvent != null)
            {
                ViewState.SelectedTrackId = selectedEvent.TrackId;
            }
            else if (_model.Tracks.Count > 0 && (!ViewState.SelectedTrackId.HasValue || GetTrack(ViewState.SelectedTrackId.Value) == null))
            {
                ViewState.SelectedTrackId = _model.Tracks[0].Id;
            }
            else if (_model.Tracks.Count == 0)
            {
                ViewState.SelectedTrackId = null;
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

    public void SetSelectedItemId(Guid? selectedEventId)
    {
        SetSelectedItemId(selectedEventId, true);
    }

    public void SetSelectedItemId(Guid? selectedEventId, bool notify)
    {
        Guid? actualSelectedEventId = selectedEventId;
        TimelineItem? selectedEvent = null;
        if (actualSelectedEventId.HasValue)
        {
            selectedEvent = FindItem(actualSelectedEventId.Value);
            if (selectedEvent == null)
            {
                actualSelectedEventId = null;
            }
        }

        if (ViewState.SelectedItemId == actualSelectedEventId)
        {
            if (selectedEvent != null && ViewState.SelectedTrackId != selectedEvent.TrackId)
            {
                SetSelectedTrackId(selectedEvent.TrackId, notify);
            }

            return;
        }

        ViewState.SelectedItemId = actualSelectedEventId;
        if (selectedEvent != null)
        {
            SetSelectedTrackId(selectedEvent.TrackId, notify);
        }

        InvalidateViewPresentation();
        if (notify)
        {
            SelectedItemChanged?.Invoke(selectedEvent);
        }
    }

    public void SetSelectedTrackId(Guid? selectedLaneId)
    {
        SetSelectedTrackId(selectedLaneId, true);
    }

    public void SetSelectedTrackId(Guid? selectedLaneId, bool notify)
    {
        Guid? actualSelectedLaneId = selectedLaneId;
        if (_model == null || _model.Tracks.Count == 0)
        {
            actualSelectedLaneId = null;
        }
        else
        {
            if (actualSelectedLaneId.HasValue && GetTrack(actualSelectedLaneId.Value) == null)
            {
                actualSelectedLaneId = null;
            }

            actualSelectedLaneId ??= _model.Tracks[0].Id;
        }

        if (ViewState.SelectedTrackId == actualSelectedLaneId)
        {
            return;
        }

        ViewState.SelectedTrackId = actualSelectedLaneId;
        _trackHeaderPanel.RefreshSelection();
        InvalidateViewPresentation();
        if (notify)
        {
            SelectedTrackChanged?.Invoke(GetSelectedTrack());
        }
    }

    internal float GetTimelineEndSeconds()
    {
        float timelineEndSeconds = Math.Max(0f, _currentTimeSeconds);
        if (_model != null)
        {
            timelineEndSeconds = Math.Max(timelineEndSeconds, Math.Max(0f, _model.DurationSeconds));
            for (var index = 0; index < _model.Items.Count; index++)
            {
                timelineEndSeconds = Math.Max(timelineEndSeconds, _model.Items[index].StartTime);
            }
        }

        float viewportWidth = _viewport.GetVisibleTimeAreaWidth();
        if (viewportWidth > 0f)
        {
            float visibleEndSeconds = ViewTransform.ViewportXToTime(viewportWidth);
            timelineEndSeconds = Math.Max(timelineEndSeconds, visibleEndSeconds);
        }

        return timelineEndSeconds > 0f ? timelineEndSeconds : 1f;
    }

    internal void NotifyTimeScrubbed(float timeSeconds)
    {
        if (Adapter != null)
        {
            Adapter.OnCurrentTimeChanged(timeSeconds);
        }
        else
        {
            TimeScrubbed?.Invoke(timeSeconds);
        }
    }

    protected void NotifyDuplicateRequested(TimelineItem timelineEvent, float timeSeconds)
    {
        float actualTime = Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds());
        if (Adapter != null)
        {
            Adapter.DuplicateItem(timelineEvent.Id, timelineEvent.TrackId, actualTime);
        }
        else
        {
            DuplicateRequested?.Invoke(timelineEvent, actualTime);
        }
    }

    protected void NotifyDeleteRequested(TimelineItem timelineEvent)
    {
        if (Adapter != null)
        {
            Adapter.DeleteItem(timelineEvent.Id);
        }
        else
        {
            DeleteRequested?.Invoke(timelineEvent);
        }
    }

    protected void NotifyCopyRequested(TimelineItem timelineEvent)
    {
        CopyRequested?.Invoke(timelineEvent);
    }

    protected void NotifyPasteRequested(TimelineTrack lane, float timeSeconds)
    {
        PasteRequested?.Invoke(lane, Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds()));
    }

    internal int GetTrackCount()
    {
        return _model?.Tracks.Count > 0 ? _model.Tracks.Count : 1;
    }

    internal Rectangle GetTrackBounds(Rectangle layoutBounds, int laneIndex)
    {
        Rectangle contentBounds = GetTrackContentBounds(layoutBounds);
        int actualLaneIndex = Math.Clamp(laneIndex, 0, GetTrackCount() - 1);
        int top = contentBounds.Top + (actualLaneIndex * TimelineControlMetrics.TrackRowHeight);
        return new Rectangle(layoutBounds.Left, top, Math.Max(1, layoutBounds.Width), TimelineControlMetrics.TrackRowHeight);
    }

    internal TimelineTrack? GetTrackAtY(Rectangle layoutBounds, int y)
    {
        if (_model == null || _model.Tracks.Count == 0)
        {
            return null;
        }

        Rectangle contentBounds = GetTrackContentBounds(layoutBounds);
        if (y < contentBounds.Top || y >= contentBounds.Bottom)
        {
            return null;
        }

        int laneIndex = Math.Clamp((y - contentBounds.Top) / TimelineControlMetrics.TrackRowHeight, 0, _model.Tracks.Count - 1);
        return _model.Tracks[laneIndex];
    }

    internal int GetTrackIndex(Guid laneId)
    {
        if (_model == null)
        {
            return -1;
        }

        for (var index = 0; index < _model.Tracks.Count; index++)
        {
            if (_model.Tracks[index].Id == laneId)
            {
                return index;
            }
        }

        return -1;
    }

    internal TimelineTrack? GetTrack(Guid laneId)
    {
        if (_model == null)
        {
            return null;
        }

        for (var index = 0; index < _model.Tracks.Count; index++)
        {
            if (_model.Tracks[index].Id == laneId)
            {
                return _model.Tracks[index];
            }
        }

        return null;
    }

    internal TimelineTrack? GetSelectedTrack()
    {
        if (_model == null || _model.Tracks.Count == 0)
        {
            return null;
        }

        if (ViewState.SelectedTrackId.HasValue)
        {
            TimelineTrack? lane = GetTrack(ViewState.SelectedTrackId.Value);
            if (lane != null)
            {
                return lane;
            }
        }

        return _model.Tracks.Count > 0 ? _model.Tracks[0] : null;
    }

    internal float SnapTime(float time, TimelineItem? item)
    {
        if (_model == null || EditPolicy == null || !SnapSettings.IsEnabled)
        {
            return time;
        }

        TimelineSnapContext context = new()
        {
            Model = _model,
            Track = item != null ? GetTrack(item.TrackId) : null,
            Item = item,
            SnapSettings = SnapSettings,
        };
        return EditPolicy.SnapTime(time, context);
    }

    internal void CommitDraggedItemTime(Guid eventId, float timeSeconds)
    {
        TimelineItem? timelineEvent = FindItem(eventId);
        if (timelineEvent == null || !timelineEvent.IsEditable)
        {
            return;
        }

        float actualTime = Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds());
        if (Math.Abs(actualTime - timelineEvent.StartTime) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        if (!IsEditAllowed(timelineEvent, actualTime, timelineEvent.Duration, isResize: false))
        {
            return;
        }

        if (Adapter != null)
        {
            Adapter.MoveItem(timelineEvent.Id, timelineEvent.TrackId, actualTime);
        }
        else
        {
            ItemTimeEditCommitted?.Invoke(timelineEvent, actualTime);
        }
    }

    internal void CommitDraggedItemResize(Guid itemId, float newStartTime, float newDuration)
    {
        TimelineItem? item = FindItem(itemId);
        if (item == null || !item.IsEditable)
        {
            return;
        }

        float actualStart = Math.Clamp(newStartTime, 0f, GetTimelineEndSeconds());
        float actualDuration = Math.Max(0f, newDuration);
        if (Math.Abs(actualStart - item.StartTime) < TimelineControlMetrics.Epsilon
            && Math.Abs(actualDuration - item.Duration) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        if (!IsEditAllowed(item, actualStart, actualDuration, isResize: true))
        {
            return;
        }

        if (Adapter != null)
        {
            Adapter.ResizeItem(item.Id, actualStart, actualDuration);
        }
        else
        {
            ItemResizeCommitted?.Invoke(item, actualStart, actualDuration);
        }
    }

    private bool IsEditAllowed(TimelineItem item, float newStartTime, float newDuration, bool isResize)
    {
        if (EditPolicy == null || _model == null)
        {
            return true;
        }

        TimelineTrack? track = GetTrack(item.TrackId);
        if (track == null)
        {
            return true;
        }

        if (isResize)
        {
            if (!EditPolicy.CanResizeItem(item, newStartTime, newDuration))
            {
                return false;
            }
        }
        else if (!EditPolicy.CanMoveItem(item, track, newStartTime))
        {
            return false;
        }

        return EditPolicy.ValidateMove(_model, item, track, newStartTime, newDuration).IsValid;
    }

    internal void DuplicateDraggedItem(Guid eventId, float timeSeconds)
    {
        TimelineItem? timelineEvent = FindItem(eventId);
        if (timelineEvent == null || !timelineEvent.IsEditable)
        {
            return;
        }

        float actualTime = Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds());
        if (Adapter != null)
        {
            Adapter.DuplicateItem(timelineEvent.Id, timelineEvent.TrackId, actualTime);
        }
        else
        {
            DuplicateRequested?.Invoke(timelineEvent, actualTime);
        }
    }

    internal bool TryInsertAtSelectedTrack(float timeSeconds)
    {
        TimelineTrack? lane = GetSelectedTrack();
        if (lane == null || !lane.IsEditable)
        {
            return false;
        }

        float actualTime = Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds());
        if (Adapter != null)
        {
            Adapter.InsertItem(lane.Id, actualTime);
        }
        else
        {
            InsertRequested?.Invoke(lane, actualTime);
        }

        return true;
    }

    internal bool TryDuplicateSelectedItem(float timeSeconds)
    {
        TimelineItem? selectedEvent = GetSelectedItem();
        if (selectedEvent == null || !selectedEvent.IsEditable)
        {
            return false;
        }

        float actualTime = Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds());
        if (Adapter != null)
        {
            Adapter.DuplicateItem(selectedEvent.Id, selectedEvent.TrackId, actualTime);
        }
        else
        {
            DuplicateRequested?.Invoke(selectedEvent, actualTime);
        }

        return true;
    }

    internal bool TryDeleteSelectedItem()
    {
        TimelineItem? selectedEvent = GetSelectedItem();
        if (selectedEvent == null || !selectedEvent.IsEditable)
        {
            return false;
        }

        if (Adapter != null)
        {
            Adapter.DeleteItem(selectedEvent.Id);
        }
        else
        {
            DeleteRequested?.Invoke(selectedEvent);
        }

        return true;
    }

    internal bool TryCopySelectedItem()
    {
        TimelineItem? selectedEvent = GetSelectedItem();
        if (selectedEvent == null || !selectedEvent.IsEditable)
        {
            return false;
        }

        CopyRequested?.Invoke(selectedEvent);
        return true;
    }

    internal bool TryPasteToSelectedTrack(float timeSeconds)
    {
        TimelineTrack? lane = GetSelectedTrack();
        if (lane == null || !lane.IsEditable)
        {
            return false;
        }

        PasteRequested?.Invoke(lane, Math.Clamp(timeSeconds, 0f, GetTimelineEndSeconds()));
        return true;
    }

    internal virtual MGContextMenu? CreateContextMenu(TimelineTrack? lane, TimelineItem? timelineEvent, float cursorTimeSeconds)
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

            string itemLabel = string.IsNullOrWhiteSpace(timelineEvent.ItemType) ? "item" : timelineEvent.ItemType;
            menu.AddButton($"Copy {itemLabel}", _ => CopyRequested?.Invoke(timelineEvent));
            menu.AddButton($"Duplicate {itemLabel} at cursor", _ => DuplicateRequested?.Invoke(timelineEvent, actualCursorTime));
            menu.AddButton($"Duplicate {itemLabel} at playhead", _ => DuplicateRequested?.Invoke(timelineEvent, CurrentTimeSeconds));
            menu.AddButton($"Delete {itemLabel}", _ => DeleteRequested?.Invoke(timelineEvent));
        }

        if (lane != null && lane.IsEditable)
        {
            if (menu.Items.Count > 0)
            {
                menu.AddSeparator();
            }

            menu.AddButton($"Paste on {lane.Label} at cursor", _ => PasteRequested?.Invoke(lane, actualCursorTime));
            menu.AddButton($"Paste on {lane.Label} at playhead", _ => PasteRequested?.Invoke(lane, CurrentTimeSeconds));
        }

        return menu.Items.Count > 0 ? menu : null;
    }

    internal virtual MGContextMenu? CreateTrackHeaderContextMenu(TimelineTrack lane)
    {
        if (ParentWindow == null)
        {
            return null;
        }

        MGContextMenu menu = new(ParentWindow, string.Empty);
        if (lane.IsEditable)
        {
            menu.AddButton("Rename track", _ => TrackRenameRequested?.Invoke(lane, lane.Label));
            menu.AddButton("Paste on track at playhead", _ => PasteRequested?.Invoke(lane, CurrentTimeSeconds));
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

    private TimelineItem? GetSelectedItem()
    {
        if (!ViewState.SelectedItemId.HasValue)
        {
            return null;
        }

        return FindItem(ViewState.SelectedItemId.Value);
    }

    private bool ContainsItem(Guid eventId)
    {
        return FindItem(eventId) != null;
    }

    private TimelineItem? FindItem(Guid eventId)
    {
        if (_model == null)
        {
            return null;
        }

        for (var index = 0; index < _model.Items.Count; index++)
        {
            if (_model.Items[index].Id == eventId)
            {
                return _model.Items[index];
            }
        }

        return null;
    }

    private Rectangle GetTrackContentBounds(Rectangle layoutBounds)
    {
        int top = layoutBounds.Top + TimelineControlMetrics.ViewportVerticalPadding;
        int height = Math.Max(1, GetTrackCount() * TimelineControlMetrics.TrackRowHeight);
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
            return TryDuplicateSelectedItem(CurrentTimeSeconds);
        }

        if (controlDown && key == Keys.C)
        {
            return TryCopySelectedItem();
        }

        if (controlDown && key == Keys.V)
        {
            return TryPasteToSelectedTrack(CurrentTimeSeconds);
        }

        switch (key)
        {
            case Keys.Insert:
                return TryInsertAtSelectedTrack(CurrentTimeSeconds);

            case Keys.Delete:
            case Keys.Back:
                return TryDeleteSelectedItem();

            case Keys.Escape:
                if (ViewState.SelectedItemId.HasValue)
                {
                    SetSelectedItemId(null, true);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }
}