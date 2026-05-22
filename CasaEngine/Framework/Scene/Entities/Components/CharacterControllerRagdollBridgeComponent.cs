using System.ComponentModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Character controller ragdoll bridge")]
public sealed class CharacterControllerRagdollBridgeComponent : EntityComponent
{
    private readonly List<PhysicsBaseComponent> _ragdollBodies = [];
    private CharacterControllerStateSnapshot _savedControllerState;
    private bool _hasSavedControllerState;

    public IReadOnlyList<PhysicsBaseComponent> RagdollBodies => _ragdollBodies;

    public bool IsRagdollActive { get; private set; }

    public bool SyncRootFromReferenceBodyOnExit { get; set; } = true;

    public bool RestoreVelocityFromReferenceBodyOnExit { get; set; }

    public override EntityComponent Clone()
    {
        return new CharacterControllerRagdollBridgeComponent
        {
            SyncRootFromReferenceBodyOnExit = SyncRootFromReferenceBodyOnExit,
            RestoreVelocityFromReferenceBodyOnExit = RestoreVelocityFromReferenceBodyOnExit,
        };
    }

    public void RegisterRagdollBody(PhysicsBaseComponent body)
    {
        ArgumentNullException.ThrowIfNull(body);

        for (int index = 0; index < _ragdollBodies.Count; index++)
        {
            if (ReferenceEquals(_ragdollBodies[index], body))
            {
                return;
            }
        }

        _ragdollBodies.Add(body);
    }

    public bool UnregisterRagdollBody(PhysicsBaseComponent body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return _ragdollBodies.Remove(body);
    }

    public void ClearRagdollBodies()
    {
        _ragdollBodies.Clear();
    }

    public void EnterRagdoll()
    {
        if (IsRagdollActive)
        {
            return;
        }

        CharacterControllerComponent controller = ResolveController();
        _savedControllerState = controller.CaptureStateSnapshot();
        _hasSavedControllerState = true;

        Vector3 controllerVelocity = controller.Velocity;
        for (int index = 0; index < _ragdollBodies.Count; index++)
        {
            PhysicsBaseComponent body = _ragdollBodies[index];
            body.SimulatePhysics = true;
            body.Velocity = controllerVelocity;
            body.SyncTransformFromScene();
        }

        controller.SetControlMode(CharacterControlMode.Disabled);
        IsRagdollActive = true;
    }

    public void ExitRagdoll()
    {
        if (!IsRagdollActive)
        {
            return;
        }

        CharacterControllerComponent controller = ResolveController();
        CharacterControllerStateSnapshot state = _hasSavedControllerState
            ? _savedControllerState
            : controller.CaptureStateSnapshot();

        if (TryGetReferenceBody(out PhysicsBaseComponent referenceBody))
        {
            if (SyncRootFromReferenceBodyOnExit)
            {
                state = state with
                {
                    Position = referenceBody.Position,
                    Orientation = referenceBody.Orientation,
                };
            }

            if (RestoreVelocityFromReferenceBodyOnExit)
            {
                state = state with { Velocity = referenceBody.Velocity };
            }
        }

        controller.RestoreStateSnapshot(state);
        IsRagdollActive = false;
        _hasSavedControllerState = false;
    }

    private CharacterControllerComponent ResolveController()
    {
        if (Owner == null)
        {
            throw new InvalidOperationException("Ragdoll bridge must be attached to an entity.");
        }

        CharacterControllerComponent controller = Owner.GetComponent<CharacterControllerComponent>();
        if (controller == null)
        {
            throw new InvalidOperationException("Ragdoll bridge requires a CharacterControllerComponent on the owner entity.");
        }

        return controller;
    }

    private bool TryGetReferenceBody(out PhysicsBaseComponent referenceBody)
    {
        for (int index = 0; index < _ragdollBodies.Count; index++)
        {
            if (_ragdollBodies[index] != null)
            {
                referenceBody = _ragdollBodies[index];
                return true;
            }
        }

        referenceBody = null!;
        return false;
    }
}