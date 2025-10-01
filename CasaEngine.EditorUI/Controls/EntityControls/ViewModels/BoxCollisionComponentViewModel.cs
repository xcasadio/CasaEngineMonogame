using System;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Core.Shapes;
using Microsoft.Xna.Framework;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GUI;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class BoxCollisionComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly BoxCollisionComponent _boxCollisionComponent;

    private const float Epsilon = 1e-5f;

    public BoxCollisionComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _boxCollisionComponent = (BoxCollisionComponent)entityComponent;
    }

    public Box Box => _boxCollisionComponent.Box;

    public Vector3 BoxSize
    {
        get => _boxCollisionComponent.Box.Size;
        set
        {
            if (_boxCollisionComponent.Box.Size != value)
            {
                _boxCollisionComponent.Box.Size = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoxWidth));
                OnPropertyChanged(nameof(BoxHeight));
                OnPropertyChanged(nameof(BoxLength));
            }
        }
    }

    public float BoxWidth
    {
        get => _boxCollisionComponent.Box.Size.X;
        set
        {
            if (Math.Abs(_boxCollisionComponent.Box.Size.X - value) > Epsilon)
            {
                _boxCollisionComponent.Box.Size = new Vector3(value, _boxCollisionComponent.Box.Size.Y, _boxCollisionComponent.Box.Size.Z);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoxSize));
            }
        }
    }

    public float BoxHeight
    {
        get => _boxCollisionComponent.Box.Size.Y;
        set
        {
            if (Math.Abs(_boxCollisionComponent.Box.Size.Y - value) > Epsilon)
            {
                _boxCollisionComponent.Box.Size = new Vector3(_boxCollisionComponent.Box.Size.X, value, _boxCollisionComponent.Box.Size.Z);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoxSize));
            }
        }
    }

    public float BoxLength
    {
        get => _boxCollisionComponent.Box.Size.Z;
        set
        {
            if (Math.Abs(_boxCollisionComponent.Box.Size.Z - value) > Epsilon)
            {
                _boxCollisionComponent.Box.Size = new Vector3(_boxCollisionComponent.Box.Size.X, _boxCollisionComponent.Box.Size.Y, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoxSize));
            }
        }
    }
}

public class Camera3dIn2dAxisComponentViewModel : Camera3dComponentViewModel
{
    private readonly Camera3dIn2dAxisComponent _camera3dIn2dAxisComponent;

    public Camera3dIn2dAxisComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _camera3dIn2dAxisComponent = (Camera3dIn2dAxisComponent)entityComponent;
    }

    public Vector3 Target
    {
        get => _camera3dIn2dAxisComponent.Target;
        set
        {
            if (_camera3dIn2dAxisComponent.Target != value)
            {
                _camera3dIn2dAxisComponent.Target = value;
                OnPropertyChanged();
            }
        }
    }
}

public class CameraLookAtComponentViewModel : Camera3dComponentViewModel
{
    private readonly CameraLookAtComponent _cameraLookAtComponent;

    public CameraLookAtComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _cameraLookAtComponent = (CameraLookAtComponent)entityComponent;
    }

    public Vector3 Target
    {
        get => _cameraLookAtComponent.Target;
        set
        {
            if (_cameraLookAtComponent.Target != value)
            {
                _cameraLookAtComponent.Target = value;
                OnPropertyChanged();
            }
        }
    }
}

public class CameraTargeted2dComponentViewModel : Camera3dComponentViewModel
{
    private readonly CameraTargeted2dComponent _cameraTargeted2dComponent;

    public CameraTargeted2dComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _cameraTargeted2dComponent = (CameraTargeted2dComponent)entityComponent;
    }

    public Vector2 DeadZoneRatio
    {
        get => _cameraTargeted2dComponent.DeadZoneRatio;
        set
        {
            if (_cameraTargeted2dComponent.DeadZoneRatio != value)
            {
                _cameraTargeted2dComponent.DeadZoneRatio = value;
                OnPropertyChanged();
            }
        }
    }

    public Rectangle Limits
    {
        get => _cameraTargeted2dComponent.Limits;
        set
        {
            if (_cameraTargeted2dComponent.Limits != value)
            {
                _cameraTargeted2dComponent.Limits = value;
                OnPropertyChanged();
            }
        }
    }

    public Entity? Target
    {
        get => _cameraTargeted2dComponent.Target;
        set
        {
            if (!Equals(_cameraTargeted2dComponent.Target, value))
            {
                _cameraTargeted2dComponent.Target = value;
                OnPropertyChanged();
            }
        }
    }
}

public class CapsuleCollisionComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly CapsuleCollisionComponent _capsuleCollisionComponent;
    private const float Epsilon = 1e-5f;

    public CapsuleCollisionComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _capsuleCollisionComponent = (CapsuleCollisionComponent)entityComponent;
    }

    public Capsule Capsule => _capsuleCollisionComponent.Capsule;

    public float Radius
    {
        get => _capsuleCollisionComponent.Capsule.Radius;
        set
        {
            if (Math.Abs(_capsuleCollisionComponent.Capsule.Radius - value) > Epsilon)
            {
                _capsuleCollisionComponent.Capsule.Radius = value;
                OnPropertyChanged();
            }
        }
    }

    public float Length
    {
        get => _capsuleCollisionComponent.Capsule.Length;
        set
        {
            if (Math.Abs(_capsuleCollisionComponent.Capsule.Length - value) > Epsilon)
            {
                _capsuleCollisionComponent.Capsule.Length = value;
                OnPropertyChanged();
            }
        }
    }
}

public class CircleCollisionComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly CircleCollisionComponent _circleCollisionComponent;
    private const float Epsilon = 1e-5f;

    public CircleCollisionComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _circleCollisionComponent = (CircleCollisionComponent)entityComponent;
    }

    public ShapeCircle Circle => _circleCollisionComponent.Circle;

    public float Radius
    {
        get => _circleCollisionComponent.Circle.Radius;
        set
        {
            if (Math.Abs(_circleCollisionComponent.Circle.Radius - value) > Epsilon)
            {
                _circleCollisionComponent.Circle.Radius = value;
                OnPropertyChanged();
            }
        }
    }
}

public class CylinderCollisionComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly CylinderCollisionComponent _cylinderCollisionComponent;
    private const float Epsilon = 1e-5f;

    public CylinderCollisionComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _cylinderCollisionComponent = (CylinderCollisionComponent)entityComponent;
    }

    public Cylinder Cylinder => _cylinderCollisionComponent.Cylinder;

    public float Radius
    {
        get => _cylinderCollisionComponent.Cylinder.Radius;
        set
        {
            if (Math.Abs(_cylinderCollisionComponent.Cylinder.Radius - value) > Epsilon)
            {
                _cylinderCollisionComponent.Cylinder.Radius = value;
                OnPropertyChanged();
            }
        }
    }

    public float Length
    {
        get => _cylinderCollisionComponent.Cylinder.Length;
        set
        {
            if (Math.Abs(_cylinderCollisionComponent.Cylinder.Length - value) > Epsilon)
            {
                _cylinderCollisionComponent.Cylinder.Length = value;
                OnPropertyChanged();
            }
        }
    }
}

public class PlayerStartComponentViewModel : SceneComponentViewModel
{
    private readonly PlayerStartComponent _playerStartComponent;

    public PlayerStartComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _playerStartComponent = (PlayerStartComponent)entityComponent;
    }
}

public class ScreenWidgetComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly ScreenWidgetComponent _screenWidgetComponent;

    public ScreenWidgetComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _screenWidgetComponent = (ScreenWidgetComponent)entityComponent;
    }

    public ScreenGui? ScreenGui => _screenWidgetComponent.ScreenGui;
}

public class SphereCollisionComponentViewModel : PhysicsBaseComponentViewModel
{
    private readonly SphereCollisionComponent _sphereCollisionComponent;
    private const float Epsilon = 1e-5f;

    public SphereCollisionComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _sphereCollisionComponent = (SphereCollisionComponent)entityComponent;
    }

    public Sphere Sphere => _sphereCollisionComponent.Sphere;

    public float Radius
    {
        get => _sphereCollisionComponent.Sphere.Radius;
        set
        {
            if (Math.Abs(_sphereCollisionComponent.Sphere.Radius - value) > Epsilon)
            {
                _sphereCollisionComponent.Sphere.Radius = value;
                OnPropertyChanged();
            }
        }
    }
}
