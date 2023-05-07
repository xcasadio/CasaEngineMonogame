using System;
using System.Windows;
using Point = Microsoft.Xna.Framework.Point;

namespace CasaEngine.WpfControls;

public class PointEditor : VectorEditorBase<Point?>
{
    public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(int?), typeof(PointEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(int?), typeof(PointEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static PointEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PointEditor), new FrameworkPropertyMetadata(typeof(PointEditor)));
    }

    public int? X
    {
        get => (int?)GetValue(XProperty);
        set => SetValue(XProperty, value);
    }

    public int? Y
    {
        get => (int?)GetValue(YProperty);
        set => SetValue(YProperty, value);
    }

    protected override void UpdateComponentsFromValue(Point? value)
    {
        if (value != null)
        {
            SetCurrentValue(XProperty, value.Value.X);
            SetCurrentValue(YProperty, value.Value.Y);
        }
    }

    protected override Point? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == XProperty)
        {
            return X.HasValue && Value.HasValue ? new Point(X.Value, Value.Value.Y) : null;
        }

        if (property == YProperty)
        {
            return Y.HasValue && Value.HasValue ? new Point(Value.Value.X, Y.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Point? UpdateValueFromFloat(float value)
    {
        var integer = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return new Point(integer, integer);
    }
}