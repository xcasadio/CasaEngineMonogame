using System;
using System.Windows;
using Microsoft.Xna.Framework;

namespace CasaEngine.WpfControls;

public class RectangleEditor : VectorEditorBase<Rectangle?>
{
    public static readonly DependencyProperty RectangleXProperty = DependencyProperty.Register(nameof(RectangleX), typeof(int?), typeof(RectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty RectangleYProperty = DependencyProperty.Register(nameof(RectangleY), typeof(int?), typeof(RectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty RectangleWidthProperty = DependencyProperty.Register(nameof(RectangleWidth), typeof(int?), typeof(RectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty RectangleHeightProperty = DependencyProperty.Register(nameof(RectangleHeight), typeof(int?), typeof(RectangleEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static RectangleEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RectangleEditor), new FrameworkPropertyMetadata(typeof(RectangleEditor)));
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

    protected override void UpdateComponentsFromValue(Rectangle? value)
    {
        if (value != null)
        {
            SetCurrentValue(RectangleXProperty, value.Value.X);
            SetCurrentValue(RectangleYProperty, value.Value.Y);
            SetCurrentValue(RectangleWidthProperty, value.Value.Width);
            SetCurrentValue(RectangleHeightProperty, value.Value.Height);
        }
    }

    /// <inheritdoc/>
    protected override Rectangle? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == RectangleXProperty)
        {
            return RectangleX.HasValue && Value.HasValue ? new Rectangle(RectangleX.Value, Value.Value.Y, Value.Value.Width, Value.Value.Height) : null;
        }

        if (property == RectangleYProperty)
        {
            return RectangleY.HasValue && Value.HasValue ? new Rectangle(Value.Value.X, RectangleY.Value, Value.Value.Width, Value.Value.Height) : null;
        }

        if (property == RectangleWidthProperty)
        {
            return RectangleWidth.HasValue && Value.HasValue ? new Rectangle(Value.Value.X, Value.Value.Y, RectangleWidth.Value, Value.Value.Height) : null;
        }

        if (property == RectangleHeightProperty)
        {
            return RectangleHeight.HasValue && Value.HasValue ? new Rectangle(Value.Value.X, Value.Value.Y, Value.Value.Width, RectangleHeight.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Rectangle? UpdateValueFromFloat(float value)
    {
        var integer = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return new Rectangle(integer, integer, integer, integer);
    }
}