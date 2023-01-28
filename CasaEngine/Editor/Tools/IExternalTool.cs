using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Editor.Tools
{
    /// <summary>
    /// 
    /// </summary>
    public interface IExternalTool
        : IContainerControl // System.Windows.Form 
    {
        /// <summary>
        /// 
        /// </summary>
        ExternalTool ExternalTool { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <param name="obj_"></param>
        void SetCurrentObject(string path_, BaseObject obj_);
    }
}
