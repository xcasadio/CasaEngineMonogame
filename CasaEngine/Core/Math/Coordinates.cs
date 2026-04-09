using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Math;

public class Coordinates
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
            _scale = value;
            SetDirtyMatrix();
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Coordinates()
    {
        Scale = Vector3.One;
        Orientation = Quaternion.Identity;
        Position = Vector3.Zero;
        SetDirtyMatrix();
    }

    public Coordinates(Coordinates other)
    {
        CopyFrom(other);
    }

    public void CopyFrom(Coordinates other)
    {
        _scale = other._scale;
        _orientation = other._orientation;
        _position = other._position;
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

    public event EventHandler? PositionChanged;
    public event EventHandler? OrientationChanged;
    public event EventHandler? ScaleChanged;
}