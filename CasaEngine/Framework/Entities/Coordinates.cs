using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

public class Coordinates
{
    private Matrix _worldMatrix;
    private Vector3 _localCenterOfRotation;
    private Vector3 _localPosition;
    private Quaternion _localRotation;
    private Vector3 _localScale;
    private bool _localMatrixChanged = true;

    [Browsable(false)]
    public Coordinates? Parent { private get; set; }

    [Category("Coordinates")]
    private Matrix LocalMatrix { get; set; }

    [Category("Coordinates")]
    public Matrix WorldMatrix
    {
        get
        {
            UpdateWorldMatrix();
            return _worldMatrix;
        }
        private set => _worldMatrix = value;
    }

    [Category("Coordinates")]
    public Vector3 LocalCenterOfRotation
    {
        get => _localCenterOfRotation;
        set
        {
            _localCenterOfRotation = value;
            SetDirtyLocalMatrix();
        }
    }

    [Category("Coordinates")]
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localPosition = value;
            SetDirtyLocalMatrix();
        }
    }

    [Category("Coordinates")]
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localRotation = value;
            SetDirtyLocalMatrix();
        }
    }

    [Category("Coordinates")]
    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localScale = value;
            SetDirtyLocalMatrix();
        }
    }

    [Category("Coordinates")]
    public Vector3 Position => Parent == null ? LocalPosition : LocalPosition + Parent.Position;

    [Category("Coordinates")]
    public Quaternion Rotation => Parent == null ? LocalRotation : LocalRotation + Parent.Rotation;

    [Category("Coordinates")]
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