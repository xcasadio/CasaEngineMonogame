using System;
using System.Windows;
using Microsoft.Xna.Framework;
using CasaEngine.Core;
using CasaEngine.Core.Helper;

namespace CasaEngine.WpfControls;

public class RotationEditor : VectorEditorBase<Quaternion?>
{
    private Vector3 _decomposedRotation;

    public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(float?), typeof(RotationEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(float?), typeof(RotationEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));
    public static readonly DependencyProperty ZProperty = DependencyProperty.Register(nameof(Z), typeof(float?), typeof(RotationEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, CoerceComponentValue));

    static RotationEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RotationEditor), new FrameworkPropertyMetadata(typeof(RotationEditor)));
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

    public override void ResetValue()
    {
        Value = Quaternion.Identity;
    }

    protected override void UpdateComponentsFromValue(Quaternion? value)
    {
        if (value != null)
        {
            // This allows iterating on the euler angles when resulting rotation are equivalent (see PDX-1779).
            var current = Recompose(ref _decomposedRotation);
            if (current == value.Value && X.HasValue && Y.HasValue && Z.HasValue)
            {
                return;
            }

            var rotationMatrix = Matrix.CreateFromQuaternion(value.Value);
            rotationMatrix.Decompose(out _decomposedRotation.Y, out _decomposedRotation.X, out _decomposedRotation.Z);
            SetCurrentValue(XProperty, GetDisplayValue(_decomposedRotation.X));
            SetCurrentValue(YProperty, GetDisplayValue(_decomposedRotation.Y));
            SetCurrentValue(ZProperty, GetDisplayValue(_decomposedRotation.Z));
        }
    }

    protected override Quaternion? UpdateValueFromComponent(DependencyProperty property)
    {
        Vector3? newDecomposedRotation;
        if (property == XProperty)
        {
            newDecomposedRotation = X.HasValue ? new Vector3(MathUtils.DegreesToRadians(X.Value), _decomposedRotation.Y, _decomposedRotation.Z) : null;
        }
        else if (property == YProperty)
        {
            newDecomposedRotation = Y.HasValue ? new Vector3(_decomposedRotation.X, MathUtils.DegreesToRadians(Y.Value), _decomposedRotation.Z) : null;
        }
        else if (property == ZProperty)
        {
            newDecomposedRotation = Z.HasValue ? new Vector3(_decomposedRotation.X, _decomposedRotation.Y, MathUtils.DegreesToRadians(Z.Value)) : null;
        }
        else
        {
            throw new ArgumentException("Property unsupported by method UpdateValueFromComponent.");
        }

        if (newDecomposedRotation.HasValue)
        {
            _decomposedRotation = newDecomposedRotation.Value;
            return Recompose(ref _decomposedRotation);
        }
        return null;
    }

    protected override Quaternion? UpdateValueFromFloat(float value)
    {
        var radian = MathUtils.DegreesToRadians(value);
        _decomposedRotation = new Vector3(radian);
        return Recompose(ref _decomposedRotation);
    }

    private static Quaternion Recompose(ref Vector3 vector)
    {
        return Quaternion.CreateFromYawPitchRoll(vector.Y, vector.X, vector.Z);
    }

    private static float GetDisplayValue(float angleRadians)
    {
        var degrees = MathUtils.RadiansToDegrees(angleRadians);
        if (degrees == 0 && float.IsNegative(degrees))
        {
            // Matrix.DecomposeXYZ can give -0 when MathF.Asin(-0) == -0,
            // whereas previously Math.Asin(-0) == +0 (ie. did not respect the sign value at zero).
            // This shows up in the editor but we don't want to see this.
            degrees = 0;
        }
        return degrees;
    }
}