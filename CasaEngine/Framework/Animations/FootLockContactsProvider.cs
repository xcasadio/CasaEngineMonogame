namespace CasaEngine.Framework.Animations;

/// <summary>
/// Fills one ground-contact flag per foot (in <see cref="FootLockController.Feet"/> order) for the
/// pose about to be solved. Invoked by <see cref="Scene.Entities.Components.SkinnedMeshComponent"/>
/// from inside the animation runtime's pose post-processing, i.e. once this frame's animation time
/// has advanced and the animated pose has been evaluated, but before any IK constraint runs.
/// </summary>
public delegate void FootLockContactsProvider(Span<bool> contacts);
