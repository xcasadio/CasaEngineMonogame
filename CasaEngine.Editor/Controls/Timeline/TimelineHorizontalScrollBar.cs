using CasaEngine.Editor.Styling;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineHorizontalScrollBar : MGSlider
{
    public TimelineHorizontalScrollBar(MGWindow window)
        : base(window, 0f, 0f, 0f)
    {
        Orientation = Orientation.Horizontal;
        MinHeight = TimelineControlMetrics.ScrollBarRowHeight;
        PreferredHeight = TimelineControlMetrics.ScrollBarRowHeight;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;
        ShowValueLabel = false;
        DrawTicks = false;
        NumberLineSize = 6;
        ThumbWidth = 18;
        ThumbHeight = 12;
        NumberLineBorderBrush = MGUniformBorderBrush.Black;
        NumberLineFillBrush = new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground);
        ThumbFillBrush = new MGSolidFillBrush(EditorThemePalette.AccentSelection);
        ThumbBorderBrush = MGUniformBorderBrush.Black;
        Visibility = Visibility.Collapsed;
    }
}