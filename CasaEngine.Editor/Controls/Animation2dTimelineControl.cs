#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dTimelineControl : MGElement
{
    private readonly List<TickLabel> _tickLabels = new();
    private readonly MGToolTip _eventToolTip;

    private IReadOnlyList<AnimationEventAsset>? _events;
    private float _durationSeconds;
    private float _currentTimeSeconds;
    private int _hoveredEventIndex = -1;
    private int _selectedEventIndex = -1;
    private float _pixelsPerSecond = DefaultPixelsPerSecond;
    private float _timelineEndSeconds = 1f;
    private float _majorTickStepSeconds = 1f;
    private float _minorTickStepSeconds = 0.2f;

    private const int ControlHeight = 104;
    private const int MinimumWidth = 320;
    private const int LeftPadding = 24;
    private const int RightPadding = 32;
    private const int TopPadding = 8;
    private const int BottomPadding = 10;
    private const int RulerHeight = 28;
    private const int TrackTopGap = 10;
    private const int TrackHeight = 40;
    private const int TrackHorizontalInset = 8;
    private const int TrackVerticalInset = 8;
    private const int TrackLabelFontSize = 11;
    private const int LabelFontSize = 11;
    private const float DefaultPixelsPerSecond = 96f;
    public const float MinimumPixelsPerSecond = 48f;
    public const float MaximumPixelsPerSecond = 288f;
    private const float MajorTickTargetPixels = 64f;
    private const float EventHalfSize = 7f;
    private const float EventHitRadius = 14f;
    private const float Epsilon = 0.0001f;

    private static readonly float[] TickStepCandidates =
    {
        0.1f,
        0.25f,
        0.5f,
        1f,
        2f,
        5f,
        10f,
        15f,
        30f,
        60f,
    };

    private readonly struct TickLabel
    {
        public TickLabel(float timeSeconds, string text)
        {
            TimeSeconds = timeSeconds;
            Text = text;
        }

        public float TimeSeconds { get; }

        public string Text { get; }
    }

    private readonly struct TimelineLayoutGeometry
    {
        public TimelineLayoutGeometry(Rectangle rulerBounds, Rectangle trackBounds)
        {
            RulerBounds = rulerBounds;
            TrackBounds = trackBounds;
        }

        public Rectangle RulerBounds { get; }

        public Rectangle TrackBounds { get; }
    }

    public float PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set
        {
            float actualValue = Math.Clamp(value, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
            if (Math.Abs(_pixelsPerSecond - actualValue) < Epsilon)
            {
                return;
            }

            _pixelsPerSecond = actualValue;
            RebuildTimelineCache();
            LayoutChanged(this, true);
            NPC(nameof(PixelsPerSecond));
        }
    }

    public event Action<int>? EventSelected;

    public event Action<float>? ScrubRequested;

    public Animation2dTimelineControl(MGWindow window)
        : base(window, MGElementType.Misc)
    {
        using (BeginInitializing())
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Stretch;
            MinHeight = ControlHeight;
            PreferredHeight = ControlHeight;

            _eventToolTip = new MGToolTip(window, this, 220, 56)
            {
                ShowDelayOverride = TimeSpan.Zero,
            };

            MouseHandler.LMBReleasedInside += OnLeftMouseReleasedInside;
            MouseHandler.MovedInside += OnMouseMovedInside;
            MouseHandler.Exited += OnMouseExited;
            RebuildTimelineCache();
        }
    }

    public void SetTimelineData(IReadOnlyList<AnimationEventAsset>? events, float durationSeconds)
    {
        _events = events;
        _durationSeconds = Math.Max(0f, durationSeconds);
        _hoveredEventIndex = _events != null && _hoveredEventIndex >= 0 && _hoveredEventIndex < _events.Count ? _hoveredEventIndex : -1;
        RefreshToolTip();
        RebuildTimelineCache();
        LayoutChanged(this, true);
    }

    public void SetPlaybackState(float currentTimeSeconds, int selectedEventIndex)
    {
        _currentTimeSeconds = Math.Clamp(currentTimeSeconds, 0f, _timelineEndSeconds);
        _selectedEventIndex = selectedEventIndex;
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        float effectivePixelsPerSecond = GetEffectivePixelsPerSecond();
        int width = Math.Max(MinimumWidth, (int)MathF.Ceiling(LeftPadding + (_timelineEndSeconds * effectivePixelsPerSecond) + RightPadding));
        return new Thickness(width, ControlHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        TimelineLayoutGeometry timelineLayout = GetTimelineLayout(layoutBounds);
        Rectangle rulerBounds = timelineLayout.RulerBounds;
        Rectangle eventTrackBounds = timelineLayout.TrackBounds;

        DA.Context.FillRectangle(origin, new RectangleF(layoutBounds.X, layoutBounds.Y, layoutBounds.Width, layoutBounds.Height), EditorThemePalette.ContentBackground * DA.Opacity);
        DrawRuler(DA, origin, layoutBounds, rulerBounds);
        DrawTrack(DA, origin, layoutBounds, eventTrackBounds);
        DrawPlayhead(DA, origin, layoutBounds, rulerBounds, eventTrackBounds);
        DrawEvents(DA, origin, layoutBounds, eventTrackBounds);
    }

    private void OnLeftMouseReleasedInside(object? sender, BaseMouseReleasedEventArgs e)
    {
        if (_events == null)
        {
            return;
        }

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        if (TryGetEventIndexAtPosition(layoutPosition, out int eventIndex))
        {
            _selectedEventIndex = eventIndex;
            e.SetHandledBy(this, false);
            EventSelected?.Invoke(eventIndex);
            return;
        }

        float timeSeconds = GetTimeAtX(LayoutBounds, layoutPosition.X);
        e.SetHandledBy(this, false);
        ScrubRequested?.Invoke(timeSeconds);
    }

    private void OnMouseMovedInside(object? sender, BaseMouseMovedEventArgs e)
    {
        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.CurrentPosition);
        if (TryGetEventIndexAtPosition(layoutPosition, out int eventIndex))
        {
            if (_hoveredEventIndex != eventIndex)
            {
                _hoveredEventIndex = eventIndex;
                RefreshToolTip();
            }

            return;
        }

        if (_hoveredEventIndex < 0)
        {
            return;
        }

        _hoveredEventIndex = -1;
        RefreshToolTip();
    }

    private void OnMouseExited(object? sender, BaseMouseMovedEventArgs e)
    {
        if (_hoveredEventIndex < 0)
        {
            return;
        }

        _hoveredEventIndex = -1;
        RefreshToolTip();
    }

    private void RebuildTimelineCache()
    {
        _timelineEndSeconds = CalculateTimelineEndSeconds();
        _majorTickStepSeconds = GetMajorTickStepSeconds(GetEffectivePixelsPerSecond());
        _minorTickStepSeconds = Math.Max(_majorTickStepSeconds / 5f, 0.02f);

        _tickLabels.Clear();
        int labelCount = (int)MathF.Ceiling(_timelineEndSeconds / _majorTickStepSeconds);
        for (int index = 0; index <= labelCount; index++)
        {
            float timeSeconds = index * _majorTickStepSeconds;
            if (timeSeconds > _timelineEndSeconds + Epsilon)
            {
                break;
            }

            _tickLabels.Add(new TickLabel(timeSeconds, FormatTickLabel(timeSeconds)));
        }
    }

    private float CalculateTimelineEndSeconds()
    {
        float result = Math.Max(_durationSeconds, _currentTimeSeconds);
        if (_events != null)
        {
            for (int index = 0; index < _events.Count; index++)
            {
                result = Math.Max(result, _events[index].TimeSeconds);
            }
        }

        return result > 0f ? result : 1f;
    }

    private float GetMajorTickStepSeconds(float effectivePixelsPerSecond)
    {
        for (int index = 0; index < TickStepCandidates.Length; index++)
        {
            float tickStep = TickStepCandidates[index];
            if (tickStep * effectivePixelsPerSecond >= MajorTickTargetPixels)
            {
                return tickStep;
            }
        }

        return TickStepCandidates[TickStepCandidates.Length - 1];
    }

    private float GetEffectivePixelsPerSecond()
    {
        float minimumReadablePixelsPerSecond = (MinimumWidth - LeftPadding - RightPadding) / Math.Max(_timelineEndSeconds, Epsilon);
        return Math.Max(_pixelsPerSecond, minimumReadablePixelsPerSecond);
    }

    private TimelineLayoutGeometry GetTimelineLayout(Rectangle layoutBounds)
    {
        int width = Math.Max(1, layoutBounds.Width - LeftPadding - RightPadding);
        int contentTop = layoutBounds.Top + TopPadding;
        int contentHeight = Math.Max(1, layoutBounds.Height - TopPadding - BottomPadding);

        int rulerHeight = RulerHeight;
        int trackTopGap = TrackTopGap;
        int trackHeight = TrackHeight;
        int desiredContentHeight = RulerHeight + TrackTopGap + TrackHeight;
        if (contentHeight < desiredContentHeight)
        {
            float scale = contentHeight / (float)desiredContentHeight;
            rulerHeight = Math.Max(12, (int)MathF.Round(RulerHeight * scale));
            trackTopGap = Math.Max(2, (int)MathF.Round(TrackTopGap * scale));
            trackHeight = Math.Max(1, contentHeight - rulerHeight - trackTopGap);

            if (rulerHeight + trackTopGap + trackHeight > contentHeight)
            {
                trackTopGap = Math.Max(0, contentHeight - rulerHeight - trackHeight);
            }

            if (rulerHeight + trackTopGap + trackHeight > contentHeight)
            {
                rulerHeight = Math.Max(1, contentHeight - trackTopGap - trackHeight);
            }
        }

        Rectangle rulerBounds = new Rectangle(
            layoutBounds.Left + LeftPadding,
            contentTop,
            width,
            Math.Max(1, rulerHeight));

        int trackTop = rulerBounds.Bottom + trackTopGap;
        int remainingTrackHeight = Math.Max(1, (contentTop + contentHeight) - trackTop);
        Rectangle trackBounds = new Rectangle(
            layoutBounds.Left + LeftPadding,
            trackTop,
            width,
            Math.Min(Math.Max(1, trackHeight), remainingTrackHeight));

        return new TimelineLayoutGeometry(rulerBounds, trackBounds);
    }

    private Rectangle GetRulerBounds(Rectangle layoutBounds)
    {
        return GetTimelineLayout(layoutBounds).RulerBounds;
    }

    private Rectangle GetTrackBounds(Rectangle layoutBounds)
    {
        return GetTimelineLayout(layoutBounds).TrackBounds;
    }

    private void DrawRuler(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle rulerBounds)
    {
        Color tickColor = EditorThemePalette.PreviewSurfaceBorder * DA.Opacity;
        Color labelColor = Color.White * (EditorThemePalette.SecondaryTextOpacity * DA.Opacity);
        float baselineY = rulerBounds.Bottom;

        DA.Context.StrokeLineSegment(origin, new Vector2(rulerBounds.Left, baselineY), new Vector2(rulerBounds.Right, baselineY), tickColor, 1f);

        int tickCount = (int)MathF.Ceiling(_timelineEndSeconds / _minorTickStepSeconds);
        for (int index = 0; index <= tickCount; index++)
        {
            float timeSeconds = index * _minorTickStepSeconds;
            if (timeSeconds > _timelineEndSeconds + Epsilon)
            {
                break;
            }

            float x = GetXForTime(layoutBounds, timeSeconds);
            float tickTop = rulerBounds.Top;
            DA.Context.StrokeLineSegment(origin, new Vector2(x, tickTop), new Vector2(x, baselineY), tickColor, 1f);
        }

        if (!TryResolveFont(LabelFontSize, out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale))
        {
            return;
        }

        for (int index = 0; index < _tickLabels.Count; index++)
        {
            TickLabel label = _tickLabels[index];
            Vector2 textSize = textEngine.MeasureText(font, label.Text);
            float x = GetXForTime(layoutBounds, label.TimeSeconds) + 2f;
            float maximumX = rulerBounds.Right - textSize.X - 4f;
            if (x > maximumX)
            {
                x = maximumX;
            }

            Vector2 drawPosition = new Vector2(x, rulerBounds.Top)
                + (font.DrawOrigin * scale)
                + origin;
            DA.DT.DrawTextViaEngine(font, label.Text, drawPosition, labelColor, font.DrawOrigin, scale);
        }
    }

    private void DrawTrack(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle trackBounds)
    {
        Color fillColor = EditorThemePalette.PreviewSurfaceBackground * DA.Opacity;
        Color borderColor = EditorThemePalette.PreviewSurfaceBorder * DA.Opacity;
        Color axisColor = EditorThemePalette.PanelBorder * DA.Opacity;
        Color laneFillColor = Color.Black * (0.18f * DA.Opacity);
        Color laneHeaderColor = Color.White * (EditorThemePalette.SecondaryTextOpacity * DA.Opacity);

        DA.Context.StrokeAndFillRectangle(origin, new RectangleF(trackBounds.X, trackBounds.Y, trackBounds.Width, trackBounds.Height), borderColor, fillColor, new Thickness(1));

        Rectangle innerTrackBounds = GetInnerTrackBounds(trackBounds);
        DA.Context.StrokeAndFillRectangle(origin, new RectangleF(innerTrackBounds.X, innerTrackBounds.Y, innerTrackBounds.Width, innerTrackBounds.Height), borderColor, laneFillColor, new Thickness(1));

        float centerY = GetEventCenterY(trackBounds);
        DA.Context.StrokeLineSegment(origin, new Vector2(innerTrackBounds.Left, centerY), new Vector2(innerTrackBounds.Right, centerY), axisColor, 1f);

        if (!TryResolveFont(TrackLabelFontSize, out _, out ResolvedFont labelFont, out float labelScale))
        {
            return;
        }

        Vector2 labelPosition = new Vector2(layoutBounds.Left + 2f, centerY)
            + (labelFont.DrawOrigin * labelScale)
            + origin;
        DA.DT.DrawTextViaEngine(labelFont, "Events", labelPosition, laneHeaderColor, labelFont.DrawOrigin, labelScale);
    }

    private void DrawPlayhead(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle rulerBounds, Rectangle trackBounds)
    {
        if (_events == null)
        {
            return;
        }

        float x = GetXForTime(layoutBounds, _currentTimeSeconds);
        Color playheadColor = EditorThemePalette.InlineRenameBorder * DA.Opacity;
        DA.Context.StrokeLineSegment(origin, new Vector2(x, rulerBounds.Top), new Vector2(x, trackBounds.Bottom), playheadColor, 1.5f);
    }

    private void DrawEvents(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, Rectangle trackBounds)
    {
        if (_events == null)
        {
            return;
        }

        float centerY = GetEventCenterY(trackBounds);
        float eventHalfSize = GetEventHalfSize(trackBounds);
        for (int index = 0; index < _events.Count; index++)
        {
            AnimationEventAsset animationEvent = _events[index];
            float x = GetXForTime(layoutBounds, animationEvent.TimeSeconds);
            bool isSelected = index == _selectedEventIndex;
            bool isHovered = index == _hoveredEventIndex;

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

    private void DrawDiamond(ElementDrawArgs DA, Vector2 origin, float x, float centerY, float eventHalfSize, Color fillColor, Color strokeColor)
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

    private bool TryResolveFont(int fontSize, out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale)
    {
        textEngine = GetTextEngine();
        font = textEngine.ResolveFont(new FontSpec(ParentWindow.Desktop.DefaultFontFamily, fontSize, CustomFontStyles.Normal));
        scale = font.SuggestedScale;
        return font.IsAvailable;
    }

    private void RefreshToolTip()
    {
        if (_events == null || _hoveredEventIndex < 0 || _hoveredEventIndex >= _events.Count)
        {
            ToolTip = null;
            return;
        }

        AnimationEventAsset animationEvent = _events[_hoveredEventIndex];
        _eventToolTip.ShowDelayOverride = TimeSpan.Zero;
        _eventToolTip.SetContent(
            $"Type: {animationEvent.EventName}\nTime: {animationEvent.TimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s",
            null,
            12);
        ToolTip = _eventToolTip;
    }

    private bool TryGetEventIndexAtPosition(Point layoutPosition, out int eventIndex)
    {
        eventIndex = -1;
        if (_events == null || _events.Count == 0)
        {
            return false;
        }

        Rectangle trackBounds = GetTrackBounds(LayoutBounds);
        if (!trackBounds.Contains(layoutPosition))
        {
            return false;
        }

        float centerY = GetEventCenterY(trackBounds);
        float bestDistanceSquared = float.MaxValue;
        float hitRadiusSquared = EventHitRadius * EventHitRadius;
        for (int index = 0; index < _events.Count; index++)
        {
            float x = GetXForTime(LayoutBounds, _events[index].TimeSeconds);
            float dx = layoutPosition.X - x;
            float dy = layoutPosition.Y - centerY;
            float distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            eventIndex = index;
        }

        return eventIndex >= 0;
    }

    private bool IsMajorTick(float timeSeconds)
    {
        if (_majorTickStepSeconds <= 0f)
        {
            return false;
        }

        float ratio = timeSeconds / _majorTickStepSeconds;
        return Math.Abs(ratio - MathF.Round(ratio)) < 0.01f;
    }

    private float GetXForTime(Rectangle layoutBounds, float timeSeconds)
    {
        return layoutBounds.Left + LeftPadding + (Math.Max(0f, timeSeconds) * GetEffectivePixelsPerSecond());
    }

    private static Rectangle GetInnerTrackBounds(Rectangle trackBounds)
    {
        int horizontalInset = Math.Min(TrackHorizontalInset, Math.Max(1, trackBounds.Width / 8));
        int verticalInset = Math.Min(TrackVerticalInset, Math.Max(1, trackBounds.Height / 4));
        int width = Math.Max(1, trackBounds.Width - (horizontalInset * 2));
        int height = Math.Max(1, trackBounds.Height - (verticalInset * 2));
        return new Rectangle(
            trackBounds.Left + horizontalInset,
            trackBounds.Top + verticalInset,
            width,
            height);
    }

    private static float GetEventCenterY(Rectangle trackBounds)
    {
        return GetInnerTrackBounds(trackBounds).Center.Y;
    }

    private static float GetEventHalfSize(Rectangle trackBounds)
    {
        Rectangle innerTrackBounds = GetInnerTrackBounds(trackBounds);
        return Math.Min(EventHalfSize, Math.Max(3f, (innerTrackBounds.Height * 0.5f) - 1f));
    }

    private float GetTimeAtX(Rectangle layoutBounds, float x)
    {
        float localX = Math.Max(0f, x - layoutBounds.Left - LeftPadding);
        return Math.Clamp(localX / GetEffectivePixelsPerSecond(), 0f, _timelineEndSeconds);
    }

    private static string FormatTickLabel(float timeSeconds)
    {
        return timeSeconds.ToString("0.##", CultureInfo.InvariantCulture);
    }
}