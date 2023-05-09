using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CasaEngine.WpfControls;

public abstract class VectorEditorBase : Control
{
    public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(VectorEditorBase), new FrameworkPropertyMetadata(-1));
    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }
}

public abstract class VectorEditorBase<T> : VectorEditorBase
{
    private bool _interlock;
    private bool _templateApplied;
    private DependencyProperty _initializingProperty;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(T), typeof(VectorEditorBase<T>), new FrameworkPropertyMetadata(default(T), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValuePropertyChanged, null, false, UpdateSourceTrigger.Explicit));
    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(nameof(DefaultValue), typeof(T), typeof(VectorEditorBase<T>), new PropertyMetadata(default(T)));

    public T Value
    {
        get => (T)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public T DefaultValue
    {
        get => (T)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public override void OnApplyTemplate()
    {
        _templateApplied = false;
        base.OnApplyTemplate();
        _templateApplied = true;
    }

    public virtual void SetVectorFromValue(float value)
    {
        Value = UpdateValueFromFloat(value);
    }

    public virtual void ResetValue()
    {
        Value = DefaultValue;
    }

    protected abstract void UpdateComponentsFromValue(T value);
    protected abstract T UpdateValueFromComponent(DependencyProperty property);
    protected abstract T UpdateValueFromFloat(float value);

    private void OnValueValueChanged()
    {
        var isInitializing = !_templateApplied && _initializingProperty == null;
        if (isInitializing)
        {
            _initializingProperty = ValueProperty;
        }

        if (!_interlock)
        {
            _interlock = true;
            UpdateComponentsFromValue(Value);
            _interlock = false;
        }

        UpdateBinding(ValueProperty);
        if (isInitializing)
        {
            _initializingProperty = null;
        }
    }

    private void OnComponentPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        var isInitializing = !_templateApplied && _initializingProperty == null;
        if (isInitializing)
        {
            _initializingProperty = e.Property;
        }

        if (!_interlock)
        {
            _interlock = true;
            Value = UpdateValueFromComponent(e.Property);
            UpdateComponentsFromValue(Value);
            _interlock = false;
        }

        UpdateBinding(e.Property);
        if (isInitializing)
        {
            _initializingProperty = null;
        }
    }

    private void UpdateBinding(DependencyProperty dependencyProperty)
    {
        if (dependencyProperty != _initializingProperty)
        {
            var expression = GetBindingExpression(dependencyProperty);
            expression?.UpdateSource();
        }
    }

    protected static void OnComponentPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var editor = (VectorEditorBase<T>)sender;
        editor.OnComponentPropertyChanged(e);
    }

    protected static object CoerceComponentValue(DependencyObject sender, object basevalue)
    {
        if (basevalue == null)
        {
            return null;
        }

        var editor = (VectorEditorBase<T>)sender;
        var decimalPlaces = editor.DecimalPlaces;
        return decimalPlaces < 0 ? basevalue : MathF.Round((float)basevalue, decimalPlaces);
    }

    private static void OnValuePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var editor = (VectorEditorBase<T>)sender;
        editor.OnValueValueChanged();
    }
}