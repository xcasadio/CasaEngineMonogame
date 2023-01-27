using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor.Manipulation
{
    /// <summary>
    /// 
    /// </summary>
    public interface IDockable
    {
        /// <summary>
        /// 
        /// </summary>
        bool Selected
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        void DrawArchor();
    }
}
