using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class PhysicsBaseComponentViewModel : SceneComponentViewModel
{
    private readonly PhysicsBaseComponent _physicsBaseComponent;

    public PhysicsBaseComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _physicsBaseComponent = (PhysicsBaseComponent)entityComponent;
    }

    // PhysicsDefinition simple forward properties with notification
    public PhysicsType PhysicsType
    {
        get => _physicsBaseComponent.PhysicsDefinition.PhysicsType;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.PhysicsType != value)
            {
                _physicsBaseComponent.PhysicsDefinition.PhysicsType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDynamic));
            }
        }
    }

    public bool IsDynamic => PhysicsType != PhysicsType.Static;

    public bool SimulatePhysics
    {
        get => _physicsBaseComponent.SimulatePhysics;
        set
        {
            if (_physicsBaseComponent.SimulatePhysics != value)
            {
                _physicsBaseComponent.SimulatePhysics = value;
                OnPropertyChanged();
            }
        }
    }

    public Vector3 Velocity
    {
        get => _physicsBaseComponent.Velocity;
        set
        {
            if (_physicsBaseComponent.Velocity != value)
            {
                _physicsBaseComponent.Velocity = value;
                OnPropertyChanged();
            }
        }
    }

    public float Mass
    {
        get => _physicsBaseComponent.PhysicsDefinition.Mass;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.Mass != value)
            {
                _physicsBaseComponent.PhysicsDefinition.Mass = value;
                OnPropertyChanged();
            }
        }
    }

    public float Friction
    {
        get => _physicsBaseComponent.PhysicsDefinition.Friction;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.Friction != value)
            {
                _physicsBaseComponent.PhysicsDefinition.Friction = value;
                OnPropertyChanged();
            }
        }
    }

    public float Restitution
    {
        get => _physicsBaseComponent.PhysicsDefinition.Restitution;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.Restitution != value)
            {
                _physicsBaseComponent.PhysicsDefinition.Restitution = value;
                OnPropertyChanged();
            }
        }
    }

    public float LinearDamping
    {
        get => _physicsBaseComponent.PhysicsDefinition.LinearDamping;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.LinearDamping != value)
            {
                _physicsBaseComponent.PhysicsDefinition.LinearDamping = value;
                OnPropertyChanged();
            }
        }
    }

    public float AngularDamping
    {
        get => _physicsBaseComponent.PhysicsDefinition.AngularDamping;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AngularDamping != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AngularDamping = value;
                OnPropertyChanged();
            }
        }
    }

    public float RollingFriction
    {
        get => _physicsBaseComponent.PhysicsDefinition.RollingFriction;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.RollingFriction != value)
            {
                _physicsBaseComponent.PhysicsDefinition.RollingFriction = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ApplyGravity
    {
        get => _physicsBaseComponent.PhysicsDefinition.ApplyGravity;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.ApplyGravity != value)
            {
                _physicsBaseComponent.PhysicsDefinition.ApplyGravity = value;
                OnPropertyChanged();
            }
        }
    }

    public Vector3 AngularFactor
    {
        get => _physicsBaseComponent.PhysicsDefinition.AngularFactor;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AngularFactor != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AngularFactor = value;
                OnPropertyChanged();
            }
        }
    }

    public Vector3 LinearFactor
    {
        get => _physicsBaseComponent.PhysicsDefinition.LinearFactor;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.LinearFactor != value)
            {
                _physicsBaseComponent.PhysicsDefinition.LinearFactor = value;
                OnPropertyChanged();
            }
        }
    }

    public float AngularSleepingThreshold
    {
        get => _physicsBaseComponent.PhysicsDefinition.AngularSleepingThreshold;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AngularSleepingThreshold != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AngularSleepingThreshold = value;
                OnPropertyChanged();
            }
        }
    }

    public float LinearSleepingThreshold
    {
        get => _physicsBaseComponent.PhysicsDefinition.LinearSleepingThreshold;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.LinearSleepingThreshold != value)
            {
                _physicsBaseComponent.PhysicsDefinition.LinearSleepingThreshold = value;
                OnPropertyChanged();
            }
        }
    }

    public float AdditionalAngularDampingFactor
    {
        get => _physicsBaseComponent.PhysicsDefinition.AdditionalAngularDampingFactor;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AdditionalAngularDampingFactor != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AdditionalAngularDampingFactor = value;
                OnPropertyChanged();
            }
        }
    }

    public float AdditionalAngularDampingThresholdSqr
    {
        get => _physicsBaseComponent.PhysicsDefinition.AdditionalAngularDampingThresholdSqr;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AdditionalAngularDampingThresholdSqr != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AdditionalAngularDampingThresholdSqr = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AdditionalDamping
    {
        get => _physicsBaseComponent.PhysicsDefinition.AdditionalDamping;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AdditionalDamping != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AdditionalDamping = value;
                OnPropertyChanged();
            }
        }
    }

    public float AdditionalDampingFactor
    {
        get => _physicsBaseComponent.PhysicsDefinition.AdditionalDampingFactor;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AdditionalDampingFactor != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AdditionalDampingFactor = value;
                OnPropertyChanged();
            }
        }
    }

    public float AdditionalLinearDampingThresholdSqr
    {
        get => _physicsBaseComponent.PhysicsDefinition.AdditionalLinearDampingThresholdSqr;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.AdditionalLinearDampingThresholdSqr != value)
            {
                _physicsBaseComponent.PhysicsDefinition.AdditionalLinearDampingThresholdSqr = value;
                OnPropertyChanged();
            }
        }
    }

    public Color? DebugColor
    {
        get => _physicsBaseComponent.PhysicsDefinition.DebugColor;
        set
        {
            if (_physicsBaseComponent.PhysicsDefinition.DebugColor != value)
            {
                _physicsBaseComponent.PhysicsDefinition.DebugColor = value;
                OnPropertyChanged();
            }
        }
    }
}