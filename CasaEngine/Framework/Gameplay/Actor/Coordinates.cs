using System.ComponentModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Gameplay.Actor;

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
    public Matrix LocalMatrix { get; private set; }

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
            _localMatrixChanged = true;
            _localCenterOfRotation = value;
        }
    }

    [Category("Coordinates")]
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            _localMatrixChanged = true;
            _localPosition = value;
        }
    }

    [Category("Coordinates")]
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            _localMatrixChanged = true;
            _localRotation = value;
        }
    }

    [Category("Coordinates")]
    public Vector3 LocalScale
    {
        get => _localScale;
        set
        {
            _localMatrixChanged = true;
            _localScale = value;
        }
    }

    [Category("Coordinates")]
    public Vector3 Position => Parent == null ? LocalPosition : LocalPosition + Parent.Position;

    [Category("Coordinates")]
    public Quaternion Rotation => Parent == null ? LocalRotation : LocalRotation + Parent.Rotation;

    [Category("Coordinates")]
    public Vector3 Scale => Parent == null ? LocalScale : LocalScale + Parent.Scale;

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
            Matrix transCenter = Matrix.CreateTranslation(LocalPosition);
            Matrix trans = Matrix.CreateTranslation(LocalCenterOfRotation);
            Matrix matS = Matrix.CreateScale(LocalScale.X, LocalScale.Y, LocalScale.Z);
            Matrix matRot = Matrix.CreateFromQuaternion(LocalRotation);

            LocalMatrix = transCenter * matS * matRot * trans;
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
}