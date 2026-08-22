# Animation Foot Lock

`FootLockController` pins a foot's ankle to the world position it had when its ground contact
started, through the existing two-bone IK solver, with blend-in/out. It targets in-place-authored
locomotion clips played on a moving entity, where the stance foot would otherwise slide.

Types (`CasaEngine.Framework.Animations`): `FootLockController`, `FootLockFoot` (hip/knee/ankle
chain, `FromAnkleName(skeleton, "LeftFoot")`), `FootLockSettings`, `FootLockFootState`,
`FootLockContactsProvider`.

## Usage

```csharp
var skeleton = component.SkeletonDefinition;
var controller = new FootLockController(
    skeleton,
    new FootLockSettings { MaxLockDistance = 0.2f, RelockMaxSpeed = 0.2f }, // entity world units
    FootLockFoot.FromAnkleName(skeleton, "LeftFoot"),
    FootLockFoot.FromAnkleName(skeleton, "RightFoot"));

// The component drives the controller from inside the runtime's pose post-processing.
component.AttachFootLock(controller, contacts =>
{
    contacts[0] = leftFootPlantedThisFrame;   // one flag per foot, in controller.Feet order
    contacts[1] = rightFootPlantedThisFrame;
});

component.FootLockApplyConstraints = false;  // keep tracking, stop solving (e.g. an A/B toggle)
component.DetachFootLock();
```

`AttachFootLock` is the recommended path. The controller must observe the **animated, pre-IK**
pose of the frame; `CurrentModelPose` read after `Update` is already the IK-solved pose, and
feeding it back into `FootLockController.Update` makes the lock chase its own output (slide
measured at ~0, vertical target frozen at the pinned height). Attached, the component:

1. runs the contacts provider and `controller.Update(dt, animatedPose, WorldMatrixWithScale, contacts)`
   inside `PosePostProcessing`, before any constraint is solved (`CurrentModelPose` holds the pure
   animated pose during the callback, and the animation time has already advanced);
2. writes the resulting `TwoBoneIkConstraint`s to the slots starting at `firstConstraintIndex`, so
   they are solved in the same frame;
3. passes the elapsed time given to `component.Update`; any extra pose refresh in the same frame
   (`PlayAnimation`, `SeekAnimation`...) advances the blends by 0 s.

`ApplyFootLock(controller)` remains for manual driving, under the call-order contract documented on
`FootLockController`.

## Settings

- `BlendInSeconds` / `BlendOutSeconds`: weight ramps on contact rising/falling edges.
- `MaxLockDistance` (entity world units): drift of the animated ankle from the pin beyond which the
  lock blends out.
- `LockVertical`: `false` (default) pins X/Z only, Y follows the animation.
- `GroundHeight` / `MaxLockHeight` (model units, compared to the animated ankle's model-space Y): a
  contact rising edge only engages the lock while the ankle is within `MaxLockHeight` of
  `GroundHeight`; a contact reported higher (a target clip's first-frame contact read while the
  blended pose still follows the source clip's swing) stays pending until the foot comes down.
  Default: disabled.
- `RelockMaxSpeed` (entity world units/s): after a drift release with the contact still true, re-pin
  once the animated ankle moves slower than this in world space. `0` (default): stay free until the
  next rising edge. A speed gate rather than an immediate re-pin, so a foot that keeps moving under
  a wrong contact flag is not locked/released repeatedly.

## Controller API

- `Update(dt, animatedModelPose, entityWorld, contacts)`, `GetFootState(footIndex)`,
  `FillConstraints` / `GetConstraint`.
- `Release()`: forget the contact history and blend out active locks; call it when the contact
  source changes (clip change, transition start) so a foot still in contact re-pins where it now
  stands instead of being dragged toward the previous clip's pin. `Reset()` clears everything
  without blend.
- `TranslateLockedPositions(worldDelta)`: move the pins along with a teleported entity (treadmill
  wrap, respawn, origin shift); otherwise the pin left behind reads as a huge drift.

## Limitations

- Flat ground: the pin is a world point, no ground projection or terrain query.
- Position only: the foot's orientation is not locked.
- Two-bone IK only: an out-of-reach pin (stance leg fully extended) leaves a residual; the hips
  are not adjusted.
- One settings record for all feet of a controller.

Demo: `SkeletalAnimationBlendingDemo` (foot-lock checkbox, treadmill).
