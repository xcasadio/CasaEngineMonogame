using CasaEngine.Gameplay.Design;

namespace CasaEngine.Gameplay
{
    public interface IAttackable
        : ICollide2Dable
    {
        //ICharacter
        void DoANewAttack();
        bool CanAttackHim(IAttackable other_);
        TeamInfo TeamInfo { get; }
    }
}
