using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

public class ColorEditor : MGColorField
{
    public new Color Value
    {
        get => base.Value?.ToXnaColor() ?? Color.White;
        set => base.Value = ColorValue.FromXnaColor(value);
    }

    public new event EventHandler<Color>? ValueChanged;

    public ColorEditor(MGWindow window, Color? initialColor = null)
        : base(window, ColorValue.FromXnaColor(initialColor ?? Color.White), CreateOptions(initialColor ?? Color.White))
    {
        AllowNull = false;
        ShowFieldTextInput = false;
        FieldWidth = 24;
        FieldHeight = 24;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;

        base.ValueChanged += OnBaseValueChanged;
    }

    private static ColorPickerOptions CreateOptions(Color initialColor)
    {
        return new ColorPickerOptions
        {
            InitialValue = ColorValue.FromXnaColor(initialColor),
            ShowAlpha = true,
            ShowTextInput = true,
            DisplayFormat = ColorValueFormat.HexRgba,
            CommitMode = ColorEditCommitMode.ExplicitOkCancel,
            Constraints = new ColorPickerConstraints
            {
                AllowAlpha = true,
                AllowHdr = false,
            },
        };
    }

    private void OnBaseValueChanged(object? sender, ColorFieldValueChangedEventArgs e)
    {
        ValueChanged?.Invoke(this, (e.NewValue ?? ColorValue.FromXnaColor(Color.White)).ToXnaColor());
    }
}
