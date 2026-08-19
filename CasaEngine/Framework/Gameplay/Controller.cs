using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Framework.Gameplay;

/// <summary>
/// Controllers are non-physical objects that can possess an <see cref="Entity"/> to control
/// its actions. <see cref="PlayerController"/> is used by human players, while
/// <see cref="AIController"/> implements artificial intelligence for the entities it controls.
/// A controller takes control of an entity via <see cref="Possess"/> and relinquishes it via
/// <see cref="UnPossess"/>. When the possessed entity has a <see cref="CharacterControllerComponent"/>,
/// possession captures the component's current <see cref="CharacterControlMode"/>, applies the
/// controller's own mode, and restores the captured mode on release.
/// </summary>
public class Controller : ObjectBase
{
    private CharacterControlMode _previousControlMode;
    private bool _hasPreviousControlMode;

    public Entity Pawn { get; private set; }

    /// <summary>
    /// The <see cref="CharacterControlMode"/> this controller imposes on a possessed entity's
    /// <see cref="CharacterControllerComponent"/>, if any. Null means possession does not
    /// touch the component's control mode.
    /// </summary>
    protected virtual CharacterControlMode? PossessedControlMode => null;

    /// <summary>
    /// Takes control of <paramref name="entity"/>. If this controller already possesses another
    /// entity, it is released first. If the entity has a <see cref="CharacterControllerComponent"/>
    /// and <see cref="PossessedControlMode"/> is set, its current control mode is captured and
    /// replaced with <see cref="PossessedControlMode"/>.
    /// </summary>
    public void Possess(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (Pawn == entity)
        {
            return;
        }

        if (Pawn != null)
        {
            UnPossess();
        }

        Pawn = entity;

        if (PossessedControlMode.HasValue)
        {
            var component = entity.GetComponent<CharacterControllerComponent>();
            if (component != null)
            {
                _previousControlMode = component.ControlMode;
                _hasPreviousControlMode = true;
                component.SetControlMode(PossessedControlMode.Value);
            }
        }

        OnPossess(entity);
    }

    /// <summary>
    /// Releases control of the currently possessed entity, restoring the
    /// <see cref="CharacterControllerComponent"/> control mode captured by <see cref="Possess"/>,
    /// if any. No-op when nothing is possessed.
    /// </summary>
    public void UnPossess()
    {
        if (Pawn == null)
        {
            return;
        }

        var releasedPawn = Pawn;

        if (_hasPreviousControlMode)
        {
            var component = releasedPawn.GetComponent<CharacterControllerComponent>();
            component?.SetControlMode(_previousControlMode);
            _hasPreviousControlMode = false;
        }

        Pawn = null;

        OnUnPossess(releasedPawn);
    }

    /// <summary>Called after this controller starts possessing <paramref name="entity"/>.</summary>
    protected virtual void OnPossess(Entity entity)
    {
    }

    /// <summary>Called after this controller releases <paramref name="entity"/>.</summary>
    protected virtual void OnUnPossess(Entity entity)
    {
    }
}
