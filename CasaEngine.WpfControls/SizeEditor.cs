using System;
using System.Windows;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.WpfControls;

public class SizeEditor : VectorEditorBase<Size?>
{
    public static readonly DependencyProperty SizeWidthProperty = DependencyProperty.Register(nameof(SizeWidth), typeof(int?), typeof(SizeEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty SizeHeightProperty = DependencyProperty.Register(nameof(SizeHeight), typeof(int?), typeof(SizeEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static SizeEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SizeEditor), new FrameworkPropertyMetadata(typeof(SizeEditor)));
    }

    public int? SizeWidth
    {
        get => (int?)GetValue(SizeWidthProperty);
        set => SetValue(SizeWidthProperty, value);
    }

    public int? SizeHeight
    {
        get => (int?)GetValue(SizeHeightProperty);
        set => SetValue(SizeHeightProperty, value);
    }

    protected override void UpdateComponentsFromValue(Size? value)
    {
        if (value != null)
        {
            SetCurrentValue(SizeWidthProperty, value.Value.Width);
            SetCurrentValue(SizeHeightProperty, value.Value.Height);
        }
    }

    protected override Size? UpdateValueFromComponent(DependencyProperty property)
    {
        if (property == SizeWidthProperty)
        {
            return SizeWidth.HasValue && Value.HasValue ? new Size(SizeWidth.Value, Value.Value.Height) : null;
        }

        if (property == SizeHeightProperty)
        {
            return SizeHeight.HasValue && Value.HasValue ? new Size(Value.Value.Width, SizeHeight.Value) : null;
        }

        throw new ArgumentException($"Property {property} is unsupported by method {nameof(UpdateValueFromComponent)}");
    }

    protected override Size? UpdateValueFromFloat(float value)
    {
        var integer = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return new Size(integer, integer);
    }
}