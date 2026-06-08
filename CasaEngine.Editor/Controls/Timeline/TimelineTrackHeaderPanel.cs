#nullable enable

using System;
using Microsoft.Xna.Framework;
using CasaEngine.Editor.Styling;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineTrackHeaderPanel : MGElement
{
    private readonly TimelineControl _owner;
    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public TimelineTrackHeaderPanel(MGWindow window, TimelineControl owner)
        : base(window, MGElementType.Misc)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        MouseHandler.LMBReleasedInside += OnLeftMouseReleasedInside;
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        int desiredHeight = (TimelineControlMetrics.ViewportVerticalPadding * 2) + (_owner.GetLaneCount() * TimelineControlMetrics.TrackRowHeight);
        return new Thickness(availableSize.Width, desiredHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        DA.Context.FillRectangle(origin, new RectangleF(layoutBounds.X, layoutBounds.Y, layoutBounds.Width, layoutBounds.Height), EditorThemePalette.PreviewSurfaceBackground * DA.Opacity);

        if (_owner.Model == null || _owner.Model.Lanes.Count == 0)
        {
            DrawLabel(DA, origin, layoutBounds, _text, isSelected: false);
            return;
        }

        for (var laneIndex = 0; laneIndex < _owner.Model.Lanes.Count; laneIndex++)
        {
            TimelineLane lane = _owner.Model.Lanes[laneIndex];
            Rectangle laneBounds = _owner.GetLaneBounds(layoutBounds, laneIndex);
            bool isSelected = _owner.ViewState.SelectedLaneId == lane.Id;
            Color backgroundColor = isSelected
                ? EditorThemePalette.AccentSelection * (0.25f * DA.Opacity)
                : EditorThemePalette.PreviewSurfaceBackground * DA.Opacity;
            DA.Context.FillRectangle(origin, new RectangleF(laneBounds.X, laneBounds.Y, laneBounds.Width, laneBounds.Height), backgroundColor);

            if (laneIndex > 0)
            {
                DA.Context.StrokeLineSegment(origin, new Vector2(laneBounds.Left, laneBounds.Top), new Vector2(laneBounds.Right, laneBounds.Top), EditorThemePalette.PreviewSurfaceBorder * DA.Opacity, 1f);
            }

            DrawLabel(DA, origin, laneBounds, lane.Label, isSelected);
        }
    }

    private void DrawLabel(ElementDrawArgs DA, Vector2 origin, Rectangle bounds, string text, bool isSelected)
    {
        if (!TryResolveFont(out ITextMeasurementEngine textEngine, out ResolvedFont font, out float scale) || !font.IsAvailable)
        {
            return;
        }

        string label = string.IsNullOrWhiteSpace(text) ? _text : text;
        Vector2 textSize = textEngine.MeasureText(font, label);
        float drawX = bounds.Left + TimelineControlMetrics.HeaderPadding;
        float drawY = bounds.Top + Math.Max(0f, (bounds.Height - Math.Max(textSize.Y, font.LineHeight * scale)) * 0.5f);
        Vector2 drawPosition = new Vector2(drawX, drawY) + (font.DrawOrigin * scale) + origin;
        Color labelColor = (isSelected ? Color.White : Color.White * EditorThemePalette.SecondaryTextOpacity) * DA.Opacity;
        DA.DT.DrawTextViaEngine(font, label, drawPosition, labelColor, font.DrawOrigin, scale);
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
        if (_owner.Model == null || _owner.Model.Lanes.Count == 0)
        {
            return;
        }

        Rectangle bounds = !ActualLayoutBounds.IsEmpty ? ActualLayoutBounds : LayoutBounds;
        if (!bounds.Contains(e.Position))
        {
            return;
        }

        _owner.Focus(KeyboardFocusSource.Pointer);

        Point layoutPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        TimelineLane? lane = _owner.GetLaneAtY(bounds, layoutPosition.Y);
        if (lane == null)
        {
            return;
        }

        _owner.SetSelectedLaneId(lane.Id, true);
        _owner.SetSelectedEventId(null, true);
        e.SetHandledBy(this, false);
    }
}