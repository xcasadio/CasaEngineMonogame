using System;
using System.Windows;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class Vector3Editor : VectorEditorBase<Vector3?>
{
    public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(float?), typeof(Vector3Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(float?), typeof(Vector3Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty ZProperty = DependencyProperty.Register(nameof(Z), typeof(float?), typeof(Vector3Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static Vector3Editor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Vector3Editor), new FrameworkPropertyMetadata(typeof(Vector3Editor)));
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

    protected override void UpdateComponentsFromValue(Vector3? value)
    {
        if (value != null)
        {
            SetCurrentValue(XProperty, value.Value.X);
            SetCurrentValue(YProperty, value.Value.Y);
            SetCurrentValue(ZProperty, value.Value.Z);
        }
    }

    protected override Vector3? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == XProperty)
        {
            return X.HasValue && Value.HasValue ? new Vector3(X.Value, Value.Value.Y, Value.Value.Z) : null;
        }

        if (property == YProperty)
        {
            return Y.HasValue && Value.HasValue ? new Vector3(Value.Value.X, Y.Value, Value.Value.Z) : null;
        }

        if (property == ZProperty)
        {
            return Z.HasValue && Value.HasValue ? new Vector3(Value.Value.X, Value.Value.Y, Z.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Vector3? UpdateValueFromFloat(float value)
    {
        return new Vector3(value);
    }
}