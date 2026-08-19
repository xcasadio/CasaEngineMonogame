using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Framework.Gameplay;

/// <summary>
/// Base class for controllers that drive AI-controlled entities. Possessing an entity applies
/// <see cref="CharacterControlMode.AI"/> to its <see cref="CharacterControllerComponent"/>, if any.
/// </summary>
public class AIController : Controller
{
    protected override CharacterControlMode? PossessedControlMode => CharacterControlMode.AI;
}
