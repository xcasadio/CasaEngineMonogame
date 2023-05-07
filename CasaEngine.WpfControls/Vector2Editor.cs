using System;
using System.Windows;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class Vector2Editor : VectorEditorBase<Vector2?>
{
    public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(float?), typeof(Vector2Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(float?), typeof(Vector2Editor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static Vector2Editor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Vector2Editor), new FrameworkPropertyMetadata(typeof(Vector2Editor)));
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

    protected override void UpdateComponentsFromValue(Vector2? value)
    {
        if (value != null)
        {
            SetCurrentValue(XProperty, value.Value.X);
            SetCurrentValue(YProperty, value.Value.Y);
        }
    }

    protected override Vector2? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == XProperty)
        {
            return X.HasValue && Value.HasValue ? new Vector2(X.Value, Value.Value.Y) : null;
        }

        if (property == YProperty)
        {
            return Y.HasValue && Value.HasValue ? new Vector2(Value.Value.X, Y.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Vector2? UpdateValueFromFloat(float value)
    {
        return new Vector2(value);
    }
}