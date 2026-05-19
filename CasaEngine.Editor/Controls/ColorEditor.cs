using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Fill_Brushes;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A compound color editor composed of a colored rectangle preview and a "…" button that
/// opens a modal MGWindow containing an <see cref="MGGridColorPicker"/> for color selection.
/// </summary>
public class ColorEditor : MGStackPanel
{
    private Color _value;
    private readonly MGRectangle _colorPreview;

    /// <summary>Gets or sets the currently selected color.</summary>
    public Color Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                _colorPreview.Fill = value.AsFillBrush();
                ValueChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>Raised whenever the user picks a new color.</summary>
    public event EventHandler<Color>? ValueChanged;

    /// <param name="window">The parent <see cref="MGWindow"/> that hosts this control.</param>
    /// <param name="initialColor">Starting color value. Defaults to <see cref="Color.White"/>.</param>
    public ColorEditor(MGWindow window, Color? initialColor = null)
        : base(window, Orientation.Horizontal)
    {
        Spacing = 4;
        VerticalAlignment = VerticalAlignment.Center;

        _value = initialColor ?? Color.White;

        // ── Color preview rectangle ───────────────────────────────────────
        _colorPreview = new MGRectangle(window, 24, 16, Color.Gray, 1, _value)
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        TryAddChild(_colorPreview);

        // ── Browse button ─────────────────────────────────────────────────
        var browseButton = new MGButton(window, _ => OpenColorPicker(window));
        browseButton.SetContent(new MGTextBlock(window, "…")
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        browseButton.PreferredWidth = 28;
        TryAddChild(browseButton);
    }

    private void OpenColorPicker(MGWindow parentWindow)
    {
        const int pickerWidth = 220;
        const int pickerHeight = 140;
        var pickerWindow = EditorModalDialogHelper.CreateCenteredModalWindow(parentWindow, pickerWidth, pickerHeight, "Select Color");

        var outerStack = new MGStackPanel(pickerWindow, Orientation.Vertical)
        {
            Spacing = 6
        };

        // ── Color picker grid ─────────────────────────────────────────────
        var picker = new MGGridColorPicker(pickerWindow, 8);
        picker.SetColors(ColorPalette.Windows_16, true);
        picker.ShowSelectedColorLabel = false;
        picker.SelectedColor = _value;
        outerStack.TryAddChild(picker);

        // ── OK / Cancel button row ─────────────────────────────────────────
        var buttonRow = new MGStackPanel(pickerWindow, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new MGButton(pickerWindow, _ =>
        {
            if (picker.SelectedColor.HasValue)
            {
                Value = picker.SelectedColor.Value;
            }

            pickerWindow.TryCloseWindow();
        });
        okButton.SetContent(new MGTextBlock(pickerWindow, "OK")
        {
            HorizontalAlignment = HorizontalAlignment.Center
        });
        okButton.PreferredWidth = 60;
        buttonRow.TryAddChild(okButton);

        var cancelButton = new MGButton(pickerWindow, _ => pickerWindow.TryCloseWindow());
        cancelButton.SetContent(new MGTextBlock(pickerWindow, "Cancel")
        {
            HorizontalAlignment = HorizontalAlignment.Center
        });
        cancelButton.PreferredWidth = 60;
        buttonRow.TryAddChild(cancelButton);

        outerStack.TryAddChild(buttonRow);

        pickerWindow.SetContent(outerStack);
    }
}
