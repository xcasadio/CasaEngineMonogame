using System;
using System.Windows;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class Vector4Editor : VectorEditorBase<Vector4?>
{
    public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(float?), typeof(Vector4Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(float?), typeof(Vector4Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty ZProperty = DependencyProperty.Register(nameof(Z), typeof(float?), typeof(Vector4Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty WProperty = DependencyProperty.Register(nameof(W), typeof(float?), typeof(Vector4Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static Vector4Editor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Vector4Editor), new FrameworkPropertyMetadata(typeof(Vector4Editor)));
    }

    public float? X
    {
        get => (float?)GetValue(XProperty);
        set => SetValue(XProperty, value);
    }

    public float? Y
    {
        get => (float?)GetValue(YProperty);
        set => SetValue(YProperty, value);
    }

    public float? Z
    {
        get => (float?)GetValue(ZProperty);
        set => SetValue(ZProperty, value);
    }

    public float? W
    {
        get => (float?)GetValue(WProperty);
        set => SetValue(WProperty, value);
    }

    protected override void UpdateComponentsFromValue(Vector4? value)
    {
        if (value != null)
        {
            SetCurrentValue(XProperty, value.Value.X);
            SetCurrentValue(YProperty, value.Value.Y);
            SetCurrentValue(ZProperty, value.Value.Z);
            SetCurrentValue(WProperty, value.Value.W);
        }
    }

    /// <inheritdoc/>
    protected override Vector4? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == XProperty)
        {
            return X.HasValue && Value.HasValue ? new Vector4(X.Value, Value.Value.Y, Value.Value.Z, Value.Value.W) : null;
        }

        if (property == YProperty)
        {
            return Y.HasValue && Value.HasValue ? new Vector4(Value.Value.X, Y.Value, Value.Value.Z, Value.Value.W) : null;
        }

        if (property == ZProperty)
        {
            return Z.HasValue && Value.HasValue ? new Vector4(Value.Value.X, Value.Value.Y, Z.Value, Value.Value.W) : null;
        }

        if (property == WProperty)
        {
            return W.HasValue && Value.HasValue ? new Vector4(Value.Value.X, Value.Value.Y, Value.Value.Z, W.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Vector4? UpdateValueFromFloat(float value)
    {
        return new Vector4(value);
    }

    private static object CoerceLengthValue(DependencyObject sender, object baseValue)
    {
        baseValue = CoerceComponentValue(sender, baseValue);
        return Math.Max(0.0f, (float)baseValue);
    }

    private static Vector4 FromLength(Vector4 value, float length)
    {
        var newValue = value;
        newValue = Vector4.Normalize(newValue);
        newValue *= length;
        return newValue;
    }
}