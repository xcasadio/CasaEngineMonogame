using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

public class Vector3ColorEditor : MGColorField
{
    public new Vector3 Value
    {
        get
        {
            if (base.Value is not { } colorValue)
            {
                return Vector3.Zero;
            }

            return new Vector3(colorValue.R, colorValue.G, colorValue.B);
        }
        set => base.Value = ColorValue.FromVector3(value);
    }

    public new event EventHandler<Vector3> ValueChanged;

    public Vector3ColorEditor(MGWindow window, Vector3? initialValue = null)
        : base(window, ColorValue.FromVector3(initialValue ?? Vector3.Zero), CreateOptions(initialValue ?? Vector3.Zero))
    {
        AllowNull = false;
        ShowFieldTextInput = false;
        FieldWidth = 24;
        FieldHeight = 24;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;

        base.ValueChanged += OnBaseValueChanged;
    }

    private static ColorPickerOptions CreateOptions(Vector3 initialValue)
    {
        return new ColorPickerOptions
        {
            InitialValue = ColorValue.FromVector3(initialValue),
            ShowAlpha = false,
            ShowTextInput = true,
            IsHdr = true,
            ShowIntensity = true,
            UseExposureSlider = true,
            ShowToneMappedPreview = true,
            DisplayFormat = ColorValueFormat.Vector3,
            CommitMode = ColorEditCommitMode.ExplicitOkCancel,
            Constraints = new ColorPickerConstraints
            {
                AllowAlpha = false,
                AllowHdr = true,
                MinChannelValue = 0f,
                MaxChannelValue = 100f,
                MinIntensity = 0f,
                MaxIntensity = 100f,
            },
        };
    }

    private void OnBaseValueChanged(object sender, ColorFieldValueChangedEventArgs e)
    {
        ColorValue colorValue = e.NewValue ?? ColorValue.FromVector3(Vector3.Zero);
        ValueChanged?.Invoke(this, new Vector3(colorValue.R, colorValue.G, colorValue.B));
    }
}