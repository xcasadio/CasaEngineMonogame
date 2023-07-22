using System;
using System.Windows;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class ShapeCircleEditor : VectorEditorBase<ShapeCircle?>
{
    public static readonly DependencyProperty CircleXProperty = DependencyProperty.Register(nameof(CircleX), typeof(int?), typeof(ShapeCircleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty CircleYProperty = DependencyProperty.Register(nameof(CircleY), typeof(int?), typeof(ShapeCircleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty CircleRadiusProperty = DependencyProperty.Register(nameof(CircleRadius), typeof(int?), typeof(ShapeCircleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static ShapeCircleEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ShapeCircleEditor), new FrameworkPropertyMetadata(typeof(ShapeCircleEditor)));
    }

    public int? CircleX
    {
        get => (int?)GetValue(CircleXProperty);
        set => SetValue(CircleXProperty, value);
    }

    public int? CircleY
    {
        get => (int?)GetValue(CircleYProperty);
        set => SetValue(CircleYProperty, value);
    }

    public int? CircleRadius
    {
        get => (int?)GetValue(CircleRadiusProperty);
        set => SetValue(CircleRadiusProperty, value);
    }

    protected override void UpdateComponentsFromValue(ShapeCircle? value)
    {
        if (value != null)
        {
            SetCurrentValue(CircleXProperty, value.Position.X);
            SetCurrentValue(CircleYProperty, value.Position.Y);
            SetCurrentValue(CircleRadiusProperty, value.Radius);
        }
    }

    /// <inheritdoc/>
    protected override ShapeCircle? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == CircleXProperty)
        {
            if (CircleX.HasValue && Value != null)
            {
                Value.Position = new Vector2(CircleX.Value, Value.Position.Y);
            }

            return Value;
        }

        if (property == CircleYProperty)
        {
            if (CircleY.HasValue && Value != null)
            {
                Value.Position = new Vector2(Value.Position.X, CircleY.Value);
            }

            return Value;
        }

        if (property == CircleRadiusProperty)
        {
            if (CircleRadius.HasValue && Value != null)
            {
                Value.Radius = CircleRadius.Value;
            }

            return Value;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override ShapeCircle UpdateValueFromFloat(float value)
    {
        var integer = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return new ShapeCircle(integer);
    }
}