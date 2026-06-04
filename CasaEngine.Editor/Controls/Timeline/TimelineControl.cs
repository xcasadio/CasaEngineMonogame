#nullable enable

using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Helpers;

namespace CasaEngine.Editor.Controls.Timeline;

internal class TimelineControl : MGGrid
{
    private readonly RowDefinition _scrollBarRow;
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

    public event Action<TimelineEvent?>? SelectedEventChanged;

    public event Action<float>? TimeScrubbed;

    public TimelineControl(MGWindow window)
        : base(window)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ColumnSpacing = 0;
        RowSpacing = 0;

        AddColumn(GridLength.CreatePixelLength(TimelineControlMetrics.TrackColumnWidth));
        AddColumn(GridLength.CreateWeightedLength(1));
        AddRow(GridLength.CreatePixelLength(TimelineControlMetrics.RulerRowHeight));
        AddRow(GridLength.CreateWeightedLength(1));
        _scrollBarRow = AddRow(GridLength.CreatePixelLength(0));

        _cornerHeader = new TimelineCornerHeader(window);
        _ruler = new TimelineRuler(window, this);
        _trackHeaderPanel = new TimelineTrackHeaderPanel(window);
        _viewport = new TimelineViewport(window, this);
        _horizontalScrollBar = new TimelineHorizontalScrollBar(window);
        _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;

        TryAddChild(0, 0, _cornerHeader);
        TryAddChild(0, 1, _ruler);
        TryAddChild(1, 0, _trackHeaderPanel);
        TryAddChild(1, 1, _viewport);
        TryAddChild(2, 1, _horizontalScrollBar);

        ViewState.PixelsPerSecond = 96f;
        SyncTransformFromViewState();
        UpdateHorizontalScrollBarState();
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

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
        }
        else
        {
            _currentTimeSeconds = Math.Clamp(_currentTimeSeconds, 0f, GetTimelineEndSeconds());
            if (ViewState.SelectedEventId.HasValue && GetSelectedEvent() == null)
            {
                ViewState.SelectedEventId = null;
            }
        }

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
        if (actualSelectedEventId.HasValue && !ContainsEvent(actualSelectedEventId.Value))
        {
            actualSelectedEventId = null;
        }

        if (ViewState.SelectedEventId == actualSelectedEventId)
        {
            return;
        }

        ViewState.SelectedEventId = actualSelectedEventId;
        InvalidateViewPresentation();
        if (notify)
        {
            SelectedEventChanged?.Invoke(GetSelectedEvent());
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

    internal void InvalidateViewPresentation()
    {
        ArrangeChanged(this, true);
    }

    private void SetPixelsPerSecond(float desiredPixelsPerSecond)
    {
        float actualPixelsPerSecond = Math.Clamp(desiredPixelsPerSecond, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
        if (Math.Abs(ViewState.PixelsPerSecond - actualPixelsPerSecond) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        ViewState.PixelsPerSecond = actualPixelsPerSecond;
        SyncTransformFromViewState();
        ClampScrollToViewport();
        UpdateHorizontalScrollBarState();
        InvalidateViewPresentation();
    }

    private TimelineEvent? GetSelectedEvent()
    {
        if (!ViewState.SelectedEventId.HasValue || _model == null)
        {
            return null;
        }

        for (var index = 0; index < _model.Events.Count; index++)
        {
            TimelineEvent timelineEvent = _model.Events[index];
            if (timelineEvent.Id == ViewState.SelectedEventId.Value)
            {
                return timelineEvent;
            }
        }

        return null;
    }

    private bool ContainsEvent(Guid eventId)
    {
        if (_model == null)
        {
            return false;
        }

        for (var index = 0; index < _model.Events.Count; index++)
        {
            if (_model.Events[index].Id == eventId)
            {
                return true;
            }
        }

        return false;
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
}