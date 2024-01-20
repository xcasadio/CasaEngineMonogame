using System.Text.Json;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace CasaEngine.Framework.Entities;

public class Coordinates
{
    private Matrix _worldMatrix;
    private Vector3 _localCenterOfRotation;
    private Vector3 _localPosition;
    private Quaternion _localRotation;
    private Vector3 _localScale;
    private bool _localMatrixChanged = true;

    public Coordinates? Parent { private get; set; }

    private Matrix LocalMatrix { get; set; }

    public Matrix WorldMatrix
    {
        get
        {
            UpdateWorldMatrix();
            return _worldMatrix;
        }
        private set => _worldMatrix = value;
    }

    public Vector3 LocalCenterOfRotation
    {
        get => _localCenterOfRotation;
        set
        {
            _localCenterOfRotation = value;
            SetDirtyLocalMatrix();
        }
    }

    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localPosition = value;
            SetDirtyLocalMatrix();
        }
    }

    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localRotation = value;
            SetDirtyLocalMatrix();
        }
    }

    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localScale = value;
            SetDirtyLocalMatrix();
        }
    }

    public Vector3 Position => Parent == null ? LocalPosition : LocalPosition + Parent.Position;

    public Quaternion Rotation => Parent == null ? LocalRotation : LocalRotation + Parent.Rotation;

    public Vector3 Scale => Parent == null ? LocalScale : LocalScale * Parent.Scale;

    public Coordinates()
    {
        LocalScale = Vector3.One;
        LocalRotation = Quaternion.Identity;
        LocalMatrix = Matrix.Identity;
        _worldMatrix = Matrix.Identity;
        SetDirtyLocalMatrix();
    }

    private void SetDirtyLocalMatrix()
    {
        _localMatrixChanged = true;
    }

    private void UpdateLocalMatrix()
    {
        if (_localMatrixChanged)
        {
            var translation = Matrix.CreateTranslation(LocalPosition);
            var translationRotation = Matrix.CreateTranslation(LocalCenterOfRotation);
            var scale = Matrix.CreateScale(LocalScale);
            var rotation = Matrix.CreateFromQuaternion(LocalRotation);
            LocalMatrix = scale * rotation * translation * translationRotation;
            _localMatrixChanged = false;
        }
    }

    private void UpdateWorldMatrix()
    {
        UpdateLocalMatrix();

        if (Parent != null)
        {
            WorldMatrix = LocalMatrix * Parent.WorldMatrix;
        }
        else
        {
            WorldMatrix = LocalMatrix;
        }
    }

    public void CopyFrom(Coordinates coordinates)
    {
        Parent = coordinates.Parent;
        _worldMatrix = coordinates._worldMatrix;
        _localCenterOfRotation = coordinates._localCenterOfRotation;
        _localPosition = coordinates._localPosition;
        _localRotation = coordinates._localRotation;
        _localScale = coordinates._localScale;
        SetDirtyLocalMatrix();
    }

    public void Load(JsonElement element)
    {
        LocalPosition = element.GetProperty("position").GetVector3();
        LocalCenterOfRotation = element.GetProperty("center_of_rotation").GetVector3();
        LocalScale = element.GetProperty("scale").GetVector3();
        LocalRotation = element.GetProperty("rotation").GetQuaternion();
        SetDirtyLocalMatrix();
    }

#if EDITOR
    public void Save(JObject jObject)
    {
        var newObject = new JObject();
        LocalPosition.Save(newObject);
        jObject.Add("position", newObject);

        newObject = new JObject();
        LocalCenterOfRotation.Save(newObject);
        jObject.Add("center_of_rotation", newObject);

        newObject = new JObject();
        LocalScale.Save(newObject);
        jObject.Add("scale", newObject);

        newObject = new JObject();
        LocalRotation.Save(newObject);
        jObject.Add("rotation", newObject);
    }

#endif
}



public class Coordinates2
{
    private Vector3 _position;
    private Quaternion _rotation;
    private Vector3 _scale;
    private Matrix _matrix;
    private bool _localMatrixChanged = true;

    public event EventHandler? PositionChanged;
    public event EventHandler? OrientationChanged;
    public event EventHandler? ScaleChanged;

    public Matrix LocalMatrix
    {
        get
        {
            UpdateMatrix();
            return _matrix;
        }
        private set => _matrix = value;
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

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
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

    public Coordinates2()
    {
        Scale = Vector3.One;
        Rotation = Quaternion.Identity;
        LocalMatrix = Matrix.Identity;
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
            LocalMatrix = scale * rotation * translation;
            _localMatrixChanged = false;
        }
    }

    public Matrix GetMatrix(Matrix? parentMatrix)
    {
        if (parentMatrix != null)
        {
            return LocalMatrix * parentMatrix.Value;
        }

        return LocalMatrix;
    }

    public void CopyFrom(Coordinates2 coordinates)
    {
        _position = coordinates._position;
        _rotation = coordinates._rotation;
        _scale = coordinates._scale;
        SetDirtyMatrix();
    }

    public void Load(JsonElement element)
    {
        Position = element.GetProperty("position").GetVector3();
        Scale = element.GetProperty("scale").GetVector3();
        Rotation = element.GetProperty("rotation").GetQuaternion();
        SetDirtyMatrix();
    }

#if EDITOR

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