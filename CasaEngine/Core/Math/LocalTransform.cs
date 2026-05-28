using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Math;

public class LocalTransform
{
    private Vector3 _position;
    private Quaternion _orientation;
    private Vector3 _scale;
    private Matrix _localMatrixWithScale;
    private Matrix _localMatrixNoScale;
    private bool _localMatrixChanged = true;

    public Matrix LocalMatrixWithScale
    {
        get
        {
            UpdateMatrix();
            return _localMatrixWithScale;
        }
    }

    public Matrix LocalMatrixNoScale
    {
        get
        {
            UpdateMatrix();
            return _localMatrixNoScale;
        }
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            SetDirtyMatrix();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Quaternion Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
            {
                return;
            }

            _orientation = value;
            SetDirtyMatrix();
            OrientationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            if (_scale == value)
            {
                return;
            }

            _scale = value;
            SetDirtyMatrix();
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public LocalTransform()
    {
        _scale = Vector3.One;
        _orientation = Quaternion.Identity;
        _position = Vector3.Zero;
        SetDirtyMatrix();
    }

    public LocalTransform(LocalTransform other)
    {
        CopyFrom(other);
    }

    public void CopyFrom(LocalTransform other)
    {
        Scale = other._scale;
        Orientation = other._orientation;
        Position = other._position;
        SetDirtyMatrix();
    }

    private void SetDirtyMatrix()
    {
        _localMatrixChanged = true;
    }

    private void UpdateMatrix()
    {
        if (_localMatrixChanged)
        {
            var translation = Matrix.CreateTranslation(Position);
            var scale = Matrix.CreateScale(Scale);
            var rotation = Matrix.CreateFromQuaternion(Orientation);
            _localMatrixWithScale = scale * rotation * translation;
            _localMatrixNoScale = rotation * translation;
            _localMatrixChanged = false;
        }
    }

    public event EventHandler PositionChanged;
    public event EventHandler OrientationChanged;
    public event EventHandler ScaleChanged;
}