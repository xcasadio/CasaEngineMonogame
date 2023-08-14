using System;
using System.Windows;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class ColorEditor : VectorEditorBase<Color?>
{
    public static readonly DependencyProperty RProperty = DependencyProperty.Register(nameof(R), typeof(byte?), typeof(ColorEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty GProperty = DependencyProperty.Register(nameof(G), typeof(byte?), typeof(ColorEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty BProperty = DependencyProperty.Register(nameof(B), typeof(byte?), typeof(ColorEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty AProperty = DependencyProperty.Register(nameof(A), typeof(byte?), typeof(ColorEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static ColorEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorEditor), new FrameworkPropertyMetadata(typeof(ColorEditor)));
    }

    public byte? R
    {
        get => (byte?)GetValue(RProperty);
        set => SetValue(RProperty, value);
    }

    public byte? G
    {
        get => (byte?)GetValue(GProperty);
        set => SetValue(GProperty, value);
    }

    public byte? B
    {
        get => (byte?)GetValue(BProperty);
        set => SetValue(BProperty, value);
    }

    public byte? A
    {
        get => (byte?)GetValue(AProperty);
        set => SetValue(AProperty, value);
    }

    protected override void UpdateComponentsFromValue(Color? value)
    {
        if (value != null)
        {
            SetCurrentValue(RProperty, value.Value.R);
            SetCurrentValue(GProperty, value.Value.G);
            SetCurrentValue(BProperty, value.Value.B);
            SetCurrentValue(AProperty, value.Value.A);
        }
    }

    protected override Color? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == RProperty)
        {
            return R.HasValue && Value.HasValue ? new Color(R.Value, Value.Value.G, Value.Value.B) : null;
        }

        if (property == GProperty)
        {
            return G.HasValue && Value.HasValue ? new Color(Value.Value.R, G.Value, Value.Value.B) : null;
        }

        if (property == BProperty)
        {
            return B.HasValue && Value.HasValue ? new Color(Value.Value.R, Value.Value.G, B.Value) : null;
        }

        if (property == AProperty)
        {
            return A.HasValue && Value.HasValue ? new Color(Value.Value.R, Value.Value.G, Value.Value.B, A.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Color? UpdateValueFromFloat(float value)
    {
        return new Color((uint)value);
    }
}