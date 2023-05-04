using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Helpers;
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
            _localMatrixChanged = true;
        }
    }

    [Category("Coordinates")]
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localPosition = value;
            _localMatrixChanged = true;
        }
    }

    [Category("Coordinates")]
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localRotation = value;
            _localMatrixChanged = true;
        }
    }

    [Category("Coordinates")]
    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localScale = value;
            _localMatrixChanged = true;
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
    }

    private void UpdateLocalMatrix()
    {
        if (_localMatrixChanged)
        {
            Matrix translation = Matrix.CreateTranslation(LocalPosition);
            Matrix translationRotation = Matrix.CreateTranslation(LocalCenterOfRotation);
            Matrix scale = Matrix.CreateScale(LocalScale);
            Matrix rotation = Matrix.CreateFromQuaternion(LocalRotation);
            LocalMatrix = scale * translation * rotation * translationRotation;
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

    public void Load(JsonElement element)
    {
        LocalPosition = element.GetProperty("position").GetVector3();
        LocalCenterOfRotation = element.GetProperty("center_of_rotation").GetVector3();
        LocalScale = element.GetProperty("scale").GetVector3();
        LocalRotation = element.GetProperty("rotation").GetQuaternion();
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