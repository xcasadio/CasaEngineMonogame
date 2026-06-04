#nullable enable

using System;
using System.Globalization;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineRuler : MGElement
{
    private readonly TimelineControl _owner;

    public TimelineRuler(MGWindow window, TimelineControl owner)
        : base(window, MGElementType.Misc)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        MinHeight = TimelineControlMetrics.RulerRowHeight;
        PreferredHeight = TimelineControlMetrics.RulerRowHeight;
        IsHitTestVisible = false;
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
        DA.Context.StrokeLineSegment(origin, new Vector2(layoutBounds.Left, baselineY), new Vector2(layoutBounds.Right, baselineY), tickColor, 1f);

        float timelineEndSeconds = _owner.GetTimelineEndSeconds();
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

            string label = timeSeconds.ToString("0.##", CultureInfo.InvariantCulture);
            Vector2 textSize = textEngine.MeasureText(font, label);
            float labelX = Math.Min(x + 2f, contentRight - textSize.X - 2f);
            Vector2 drawPosition = new Vector2(labelX, layoutBounds.Top + 1f)
                + (font.DrawOrigin * scale)
                + origin;
            DA.DT.DrawTextViaEngine(font, label, drawPosition, labelColor, font.DrawOrigin, scale);
        }
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
        scale = font.SuggestedScale;
        return font.IsAvailable;
    }
}