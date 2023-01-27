using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.Tools
{
    interface IEditorForm
    {
        /// <summary>
        /// 
        /// </summary>
        Control XnaPanel
        { 
            get; 
        }
    }
}
