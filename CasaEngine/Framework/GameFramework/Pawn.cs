using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.GameFramework;

/**
 * Pawn is the base class of all actors that can be possessed by players or AI.
 * They are the physical representations of players and creatures in a level.
 *
 * @see https://docs.unrealengine.com/latest/INT/Gameplay/Framework/Pawn/
 */
public class Pawn : Entity // , INavAgentInterface
{
    public bool InputEnabled { get; set; } = true;

    public Controller Controller { get; set; }

    public Pawn()
    {

    }

    private Pawn(Pawn other) : base(other)
    {
        InputEnabled = other.InputEnabled;
    }

    public override Pawn Clone()
    {
        return new Pawn(this);
    }
}