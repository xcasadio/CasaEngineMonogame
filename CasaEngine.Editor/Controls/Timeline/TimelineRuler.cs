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

internal sealed class TimelineRuler : MGElement
{
    private readonly TimelineControl _owner;
    private bool _dragScrubbing;
    private bool _ignoreNextRelease;
    private float _lastDragTimeSeconds;

    public TimelineRuler(MGWindow window, TimelineControl owner)
        : base(window, MGElementType.Misc)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = true;
        MouseHandler.LMBReleasedInside += OnLeftMouseReleasedInside;
        MouseHandler.Scrolled += OnScrolled;
        if (_owner.RulerDragScrubEnabled)
        {
            MouseHandler.DragStartCondition = DragStartCondition.MouseMovedAfterPress;
            MouseHandler.LMBPressedInside += OnLeftMousePressedInside;
            MouseHandler.DragStart += OnDragStart;
            MouseHandler.Dragged += OnDragged;
            MouseHandler.DragEnd += OnDragEnd;
        }
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);

        float textHeight = TimelineControlMetrics.LabelFontSize;
        if (TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out _) && font.IsAvailable)
        {
            textHeight = Math.Max(textEngine.MeasureText(font, "0.5").Y, font.LineHeight);
        }

        int desiredHeight = (int)MathF.Ceiling(textHeight + 8f);
        return new Thickness(availableSize.Width, desiredHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        DA.Context.FillRectangle(origin, new RectangleF(layoutBounds.X, layoutBounds.Y, layoutBounds.Width, layoutBounds.Height), EditorThemePalette.ContentBackground * DA.Opacity);

        float contentLeft = layoutBounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft;
        float contentRight = layoutBounds.Right - TimelineControlMetrics.TimeAreaPaddingRight;
        if (contentRight <= contentLeft)
        {
            return;
        }

        Color tickColor = EditorThemePalette.PreviewSurfaceBorder * DA.Opacity;
        Color labelColor = Color.White * (EditorThemePalette.SecondaryTextOpacity * DA.Opacity);
        float baselineY = layoutBounds.Bottom - 1f;

        float timelineEndSeconds = _owner.GetTimelineEndSeconds();
        float playheadX = contentLeft + _owner.ViewTransform.TimeToViewportX(_owner.CurrentTimeSeconds);
        if (playheadX >= contentLeft - TimelineControlMetrics.Epsilon && playheadX <= contentRight + TimelineControlMetrics.Epsilon)
        {
            DrawPlayhead(DA, origin, layoutBounds, playheadX);
        }

        TimelineTimeUnit timeUnit = _owner.Model?.TimeUnit ?? TimelineTimeUnit.Seconds;
        float frameRate = _owner.Model?.FrameRate ?? 60f;
        float majorTickStep = TimelineTickCalculator.GetMajorTickStepSeconds(_owner.ViewTransform.PixelsPerSecond, TimelineControlMetrics.MajorTickTargetPixels);
        float minorTickStep = TimelineTickCalculator.GetMinorTickStepSeconds(majorTickStep);
        int startIndex = (int)MathF.Floor(Math.Max(0f, _owner.ViewTransform.ViewportXToTime(-TimelineControlMetrics.TimeAreaPaddingLeft)) / minorTickStep);
        int endIndex = (int)MathF.Ceiling(Math.Max(0f, _owner.ViewTransform.ViewportXToTime(contentRight - contentLeft + TimelineControlMetrics.TimeAreaPaddingRight)) / minorTickStep) + 1;

        TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale);

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
            float tickTop = isMajor ? layoutBounds.Top + 2f : layoutBounds.Top + 8f;
            DA.Context.StrokeLineSegment(origin, new Vector2(x, tickTop), new Vector2(x, baselineY), tickColor, 1f);

            if (!isMajor || !font.IsAvailable)
            {
                continue;
            }

            string label = TimelineTickCalculator.FormatTimeLabel(timeSeconds, timeUnit, frameRate);
            Vector2 textSize = textEngine.MeasureText(font, label);
            float textHeight = Math.Max(textSize.Y, font.LineHeight);
            float labelX = Math.Min(x + 2f, contentRight - textSize.X - 2f);
            float labelY = layoutBounds.Top + Math.Max(0f, (layoutBounds.Height - textHeight) * 0.5f);
            Vector2 drawPosition = new Vector2(labelX, labelY)
                + (font.DrawOrigin * scale)
                + origin;
            DA.DT.DrawTextViaEngine(font, label, drawPosition, labelColor, font.DrawOrigin, scale);
        }
    }

    private static void DrawPlayhead(ElementDrawArgs DA, Vector2 origin, Rectangle layoutBounds, float x)
    {
        Color playheadColor = EditorThemePalette.InlineRenameBorder * DA.Opacity;
        DA.Context.StrokeLineSegment(origin, new Vector2(x, layoutBounds.Top), new Vector2(x, layoutBounds.Bottom), playheadColor, 1.5f);
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

    private bool TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale)
    {
        textEngine = GetTextEngine();
        font = textEngine.ResolveFont(new FontSpec(ParentWindow.Desktop.DefaultFontFamily, TimelineControlMetrics.LabelFontSize, CustomFontStyles.Normal));
        scale = font.ExactScale;
        return font.IsAvailable;
    }

    private void OnScrolled(object? sender, MGUI.Shared.Input.Mouse.BaseMouseScrolledEventArgs e)
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

        _owner.Focus(KeyboardFocusSource.Pointer);

        float wheelSteps = e.ScrollWheelDelta / 120.0f;
        float anchorViewportX = Math.Clamp(e.Position.X - timeAreaLeft, 0f, Math.Max(0f, bounds.Width - TimelineControlMetrics.TimeAreaPaddingLeft - TimelineControlMetrics.TimeAreaPaddingRight));
        _owner.ApplyMouseWheelZoom(wheelSteps, anchorViewportX);
        e.SetHandledBy(this);
    }

    private void OnLeftMousePressedInside(object? sender, MGUI.Shared.Input.Mouse.BaseMousePressedEventArgs e)
    {
        // A drag that ended outside the ruler leaves no release to consume: a new press starts clean.
        _ignoreNextRelease = false;
    }

    private void OnDragStart(object? sender, MGUI.Shared.Input.Mouse.BaseMouseDragStartEventArgs e)
    {
        if (_owner.Model == null || !e.IsLMB || e.Condition != DragStartCondition.MouseMovedAfterPress)
        {
            return;
        }

        Rectangle bounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        int timeAreaLeft = bounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft;
        int timeAreaRight = bounds.Right - TimelineControlMetrics.TimeAreaPaddingRight;
        if (e.Position.X < timeAreaLeft || e.Position.X > timeAreaRight)
        {
            return;
        }

        _dragScrubbing = true;
        _lastDragTimeSeconds = float.NaN;
        _owner.Focus(KeyboardFocusSource.Pointer);
        ScrubTo(bounds, e.Position.X);
        e.SetHandledBy(this, false);
    }

    private void OnDragged(object? sender, MGUI.Shared.Input.Mouse.BaseMouseDraggedEventArgs e)
    {
        if (!_dragScrubbing || !e.IsLMB || _owner.Model == null)
        {
            return;
        }

        Rectangle bounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        ScrubTo(bounds, e.Position.X);
    }

    private void OnDragEnd(object? sender, MGUI.Shared.Input.Mouse.BaseMouseDragEndEventArgs e)
    {
        if (!_dragScrubbing || !e.IsLMB)
        {
            return;
        }

        _dragScrubbing = false;
        // The release that follows the drag must not scrub a second time.
        _ignoreNextRelease = true;
    }

    /// <summary>Moves the playhead to the time under <paramref name="x"/> (clamped to the time area) and notifies, unless the time did not change since the last move of this drag.</summary>
    private void ScrubTo(Rectangle bounds, int x)
    {
        float timeSeconds = GetTimeAtX(bounds, x);
        if (Math.Abs(timeSeconds - _lastDragTimeSeconds) < TimelineControlMetrics.Epsilon)
        {
            return;
        }

        _lastDragTimeSeconds = timeSeconds;
        _owner.SetCurrentTimeSeconds(timeSeconds, notify: false);
        _owner.NotifyTimeScrubbed(timeSeconds);
    }

    private float GetTimeAtX(Rectangle bounds, int x)
    {
        int timeAreaLeft = bounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft;
        float viewportX = Math.Clamp(x - timeAreaLeft, 0f, Math.Max(0f, bounds.Width - TimelineControlMetrics.TimeAreaPaddingLeft - TimelineControlMetrics.TimeAreaPaddingRight));
        return _owner.ViewTransform.ViewportXToTime(viewportX);
    }

    private void OnLeftMouseReleasedInside(object? sender, MGUI.Shared.Input.Mouse.BaseMouseReleasedEventArgs e)
    {
        if (_ignoreNextRelease)
        {
            _ignoreNextRelease = false;
            e.SetHandledBy(this, false);
            return;
        }

        if (_owner.Model == null)
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

        _owner.Focus(KeyboardFocusSource.Pointer);

        float timeSeconds = GetTimeAtX(bounds, e.Position.X);
        _owner.SetCurrentTimeSeconds(timeSeconds, notify: false);
        _owner.NotifyTimeScrubbed(timeSeconds);
        e.SetHandledBy(this, false);
    }
}