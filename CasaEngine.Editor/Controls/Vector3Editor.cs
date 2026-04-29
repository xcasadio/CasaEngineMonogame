using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Compound control for editing a <see cref="Vector3"/> value.
/// Renders three <see cref="NumericField"/> components labelled X, Y, Z.
/// </summary>
public class Vector3Editor : MGStackPanel
{
    private readonly NumericField _fieldX;
    private readonly NumericField _fieldY;
    private readonly NumericField _fieldZ;

    private Vector3 _value;
    private bool _suppressValueChanged;

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    public Vector3 Value
    {
        get => _value;
        set => SetValue(value, notify: false);
    }

    public float Step
    {
        get => _fieldX.Step;
        set
        {
            _fieldX.Step = value;
            _fieldY.Step = value;
            _fieldZ.Step = value;
        }
    }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    public event EventHandler<Vector3> ValueChanged;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Vector3Editor(MGWindow window, float step = 0.1f)
        : base(window, Orientation.Horizontal)
    {
        Spacing = 4;

        _fieldX = new NumericField(window, "X", step: step)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _fieldY = new NumericField(window, "Y", step: step)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _fieldZ = new NumericField(window, "Z", step: step)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Colour the labels: X=red, Y=green, Z=blue
        // MGUI TextBlock supports inline rich-text colour tags
        _fieldX.Label = "[c=Red]X[/c]";
        _fieldY.Label = "[c=Lime]Y[/c]";
        _fieldZ.Label = "[c=CornflowerBlue]Z[/c]";

        _fieldX.Step = step;
        _fieldY.Step = step;
        _fieldZ.Step = step;

        _fieldX.ValueChanged += (_, v) =>
        {
            if (_suppressValueChanged)
            {
                return;
            }

            _value.X = v;
            ValueChanged?.Invoke(this, _value);
        };
        _fieldY.ValueChanged += (_, v) =>
        {
            if (_suppressValueChanged)
            {
                return;
            }

            _value.Y = v;
            ValueChanged?.Invoke(this, _value);
        };
        _fieldZ.ValueChanged += (_, v) =>
        {
            if (_suppressValueChanged)
            {
                return;
            }

            _value.Z = v;
            ValueChanged?.Invoke(this, _value);
        };

        TryAddChild(_fieldX);
        TryAddChild(_fieldY);
        TryAddChild(_fieldZ);
    }

    private void SetValue(Vector3 value, bool notify)
    {
        if (_value == value)
        {
            return;
        }

        _value = value;

        _suppressValueChanged = true;
        try
        {
            _fieldX.Value = value.X;
            _fieldY.Value = value.Y;
            _fieldZ.Value = value.Z;
        }
        finally
        {
            _suppressValueChanged = false;
        }

        if (notify)
        {
            ValueChanged?.Invoke(this, _value);
        }
    }
}
