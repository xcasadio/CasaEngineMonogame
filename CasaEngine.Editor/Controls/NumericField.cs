using System;
using System.Globalization;
using MGUI.Core.UI;
using Thickness = MonoGame.Extended.Thickness;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A compound numeric field composed of an optional label, a text box, and two +/- buttons.
/// Supports float values with configurable Min, Max, Step and scroll-wheel increment.
/// </summary>
public class NumericField : MGStackPanel
{
    private readonly MGTextBlock _labelElement;
    private readonly MGTextBox _textBox;
    private readonly MGButton _downButton;
    private readonly MGButton _upButton;

    private float _value;
    private bool _suppressTextChanged;

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    public float Min { get; set; } = float.MinValue;
    public float Max { get; set; } = float.MaxValue;
    public float Step { get; set; } = 1f;

    public float Value
    {
        get => _value;
        set => SetValue(value, notify: true);
    }

    public string Label
    {
        get => _labelElement.Text;
        set
        {
            _labelElement.Text = value;
            _labelElement.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    public event EventHandler<float> ValueChanged;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public NumericField(MGWindow window, string label = "", float min = float.MinValue, float max = float.MaxValue, float step = 1f)
        : base(window, Orientation.Horizontal)
    {
        Min = min;
        Max = max;
        Step = step;
        Spacing = 2;

        // Label
        _labelElement = new MGTextBlock(window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Visibility = string.IsNullOrEmpty(label) ? Visibility.Collapsed : Visibility.Visible
        };

        // Down button (−)
        _downButton = new MGButton(window, _ =>
        {
            SetValue(_value - Step, notify: true);
        });
        _downButton.SetContent(new MGTextBlock(window, "−") { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        _downButton.MinWidth = 20;
        _downButton.PreferredWidth = 20;

        // Text box
        _textBox = new MGTextBox(window)
        {
            MinWidth = 60,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _textBox.TextChanged += OnTextBoxTextChanged;

        // Scroll wheel on text box
        _textBox.MouseHandler.Scrolled += OnScrolled;

        // Up button (+)
        _upButton = new MGButton(window, _ =>
        {
            SetValue(_value + Step, notify: true);
        });
        _upButton.SetContent(new MGTextBlock(window, "+") { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        _upButton.MinWidth = 20;
        _upButton.PreferredWidth = 20;

        TryAddChild(_labelElement);
        TryAddChild(_downButton);
        TryAddChild(_textBox);
        TryAddChild(_upButton);

        // Initialise display
        SetValue(0f, notify: false);
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private void SetValue(float raw, bool notify)
    {
        float clamped = Math.Clamp(raw, Min, Max);
        if (_value == clamped && !notify)
        {
            return;
        }

        _value = clamped;

        _suppressTextChanged = true;
        _textBox.SetText(_value.ToString("G7", CultureInfo.InvariantCulture));
        _suppressTextChanged = false;

        if (notify)
        {
            ValueChanged?.Invoke(this, _value);
        }
    }

    private void OnTextBoxTextChanged(object sender, EventArgs<string> e)
    {
        if (_suppressTextChanged)
        {
            return;
        }

        if (float.TryParse(e.NewValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            float clamped = Math.Clamp(parsed, Min, Max);
            if (_value != clamped)
            {
                _value = clamped;
                ValueChanged?.Invoke(this, _value);
            }
        }
        // Invalid text: silently ignore — value stays unchanged
    }

    private void OnScrolled(object sender, BaseMouseScrolledEventArgs e)
    {
        float delta = e.ScrollWheelDelta > 0 ? Step : -Step;
        SetValue(_value + delta, notify: true);
    }
}
