#nullable enable

using System;
using CasaEngine.Editor.Styling;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineCornerHeader : MGBorder
{
    private readonly MGTextBlock _textBlock;

    public string Text
    {
        get => _textBlock.Text;
        set => _textBlock.Text = value ?? string.Empty;
    }

    public TimelineCornerHeader(MGWindow window)
        : base(window)
    {
        BorderThickness = new Thickness(0);
        BorderBrush = new MGUniformBorderBrush(EditorThemePalette.PreviewSurfaceBorder);
        BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.ContentBackground));
        Padding = new Thickness(TimelineControlMetrics.HeaderPadding, TimelineControlMetrics.HeaderVerticalPadding, TimelineControlMetrics.HeaderPadding, TimelineControlMetrics.HeaderVerticalPadding);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _textBlock = new MGTextBlock(window, "Tracks")
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = false,
        };
        SetContent(_textBlock);
    }
}