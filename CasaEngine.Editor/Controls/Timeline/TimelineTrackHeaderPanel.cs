#nullable enable

using System;
using CasaEngine.Editor.Styling;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineTrackHeaderPanel : MGBorder
{
    private readonly MGTextBlock _textBlock;

    public string Text
    {
        get => _textBlock.Text;
        set => _textBlock.Text = value ?? string.Empty;
    }

    public TimelineTrackHeaderPanel(MGWindow window)
        : base(window)
    {
        BorderThickness = new Thickness(1);
        BorderBrush = new MGUniformBorderBrush(EditorThemePalette.PreviewSurfaceBorder);
        BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground));
        Padding = new Thickness(TimelineControlMetrics.HeaderPadding, TimelineControlMetrics.HeaderVerticalPadding, TimelineControlMetrics.HeaderPadding, TimelineControlMetrics.HeaderVerticalPadding);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _textBlock = new MGTextBlock(window, "Track 01")
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = false,
        };
        SetContent(_textBlock);
    }
}