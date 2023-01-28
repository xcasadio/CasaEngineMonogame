using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Design;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public interface IAttackable
        : ICollide2Dable
    {
        //ICharacter
        void DoANewAttack();
        bool CanAttackHim(IAttackable other_);
        TeamInfo TeamInfo { get; }
    }
}
