using CasaEngine.Framework.Gameplay.Design;

namespace CasaEngine.Framework.Gameplay;

public interface IAttackable
    : ICollide2Dable
{
    //ICharacter
    void DoANewAttack();
    bool CanAttackHim(IAttackable other);
    TeamInfo TeamInfo { get; }
}