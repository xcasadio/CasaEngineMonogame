using System.Text.Json;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

public class Coordinates
{
    private Vector3 _position;
    private Quaternion _rotation;
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
#if EDITOR
            PositionChanged?.Invoke(this, EventArgs.Empty);
#endif
        }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            SetDirtyMatrix();
#if EDITOR
            OrientationChanged?.Invoke(this, EventArgs.Empty);
#endif
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            SetDirtyMatrix();
#if EDITOR
            ScaleChanged?.Invoke(this, EventArgs.Empty);
#endif
        }
    }

    public Coordinates()
    {
        Scale = Vector3.One;
        Rotation = Quaternion.Identity;
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
        _rotation = other._rotation;
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
            var rotation = Matrix.CreateFromQuaternion(Rotation);
            _localMatrixWithScale = scale * rotation * translation;
            _localMatrixNoScale = rotation * translation;
            _localMatrixChanged = false;
        }
    }

    public void Load(JsonElement element)
    {
        Position = element.GetProperty("position").GetVector3();
        Scale = element.GetProperty("scale").GetVector3();
        Rotation = element.GetProperty("rotation").GetQuaternion();
        SetDirtyMatrix();
    }

#if EDITOR
    public event EventHandler? PositionChanged;
    public event EventHandler? OrientationChanged;
    public event EventHandler? ScaleChanged;

    public void Save(JObject jObject)
    {
        var newObject = new JObject();
        Position.Save(newObject);
        jObject.Add("position", newObject);

        newObject = new JObject();
        Scale.Save(newObject);
        jObject.Add("scale", newObject);

        newObject = new JObject();
        Rotation.Save(newObject);
        jObject.Add("rotation", newObject);
    }

#endif
}