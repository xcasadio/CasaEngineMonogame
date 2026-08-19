using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Framework.Gameplay;

/// <summary>
/// Base entity class historically used for player/AI-controllable characters.
/// Kept for asset compatibility: <c>PlayerStartupSettings.DefaultPawnAssetId</c>
/// spawns one and an asset loader is registered for it. Possession itself
/// works on any <see cref="Entity"/> via <see cref="Controller.Possess"/>.
/// </summary>
public class Pawn : Entity
{
    public Pawn()
    {

    }

    private Pawn(Pawn other) : base(other)
    {
    }

    public override Pawn Clone()
    {
        return new Pawn(this);
    }
}
