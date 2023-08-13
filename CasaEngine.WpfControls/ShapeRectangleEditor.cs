using System;
using System.Windows;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class ShapeRectangleEditor : VectorEditorBase<ShapeRectangle?>
{
    public static readonly DependencyProperty RectangleXProperty = DependencyProperty.Register(nameof(RectangleX), typeof(int?), typeof(ShapeRectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty RectangleYProperty = DependencyProperty.Register(nameof(RectangleY), typeof(int?), typeof(ShapeRectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty RectangleWidthProperty = DependencyProperty.Register(nameof(RectangleWidth), typeof(int?), typeof(ShapeRectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty RectangleHeightProperty = DependencyProperty.Register(nameof(RectangleHeight), typeof(int?), typeof(ShapeRectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static ShapeRectangleEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ShapeRectangleEditor), new FrameworkPropertyMetadata(typeof(ShapeRectangleEditor)));
    }

    public int? RectangleX
    {
        get => (int?)GetValue(RectangleXProperty);
        set => SetValue(RectangleXProperty, value);
    }

    public int? RectangleY
    {
        get => (int?)GetValue(RectangleYProperty);
        set => SetValue(RectangleYProperty, value);
    }

    public int? RectangleWidth
    {
        get => (int?)GetValue(RectangleWidthProperty);
        set => SetValue(RectangleWidthProperty, value);
    }

    public int? RectangleHeight
    {
        get => (int?)GetValue(RectangleHeightProperty);
        set => SetValue(RectangleHeightProperty, value);
    }

    protected override void UpdateComponentsFromValue(ShapeRectangle? value)
    {
        if (value != null)
        {
            SetCurrentValue(RectangleXProperty, (int)value.Position.X);
            SetCurrentValue(RectangleYProperty, (int)value.Position.Y);
            SetCurrentValue(RectangleWidthProperty, value.Width);
            SetCurrentValue(RectangleHeightProperty, value.Height);
        }
    }

    protected override ShapeRectangle? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == RectangleXProperty)
        {
            if (RectangleX.HasValue && Value != null)
            {
                Value.Position = new Vector2(RectangleX.Value, Value.Position.Y);
            }

            return Value;
        }

        if (property == RectangleYProperty)
        {
            if (RectangleY.HasValue && Value != null)
            {
                Value.Position = new Vector2(Value.Position.X, RectangleY.Value);
            }

            return Value;
        }

        if (property == RectangleWidthProperty)
        {
            if (RectangleWidth.HasValue && Value != null)
            {
                Value.Width = RectangleWidth.Value;
            }

            return Value;
        }

        if (property == RectangleHeightProperty)
        {
            if (RectangleHeight.HasValue && Value != null)
            {
                Value.Height = RectangleHeight.Value;
            }

            return Value;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override ShapeRectangle UpdateValueFromFloat(float value)
    {
        var integer = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return new ShapeRectangle(integer, integer, integer, integer);
    }
}