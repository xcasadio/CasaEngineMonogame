using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor.UndoRedo
{
    /// <summary>
    /// Design pattern Command
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        void Execute(object arg1_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        void Undo(object arg1_);
    }
}
