using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CasaEngine.Editor.Tools
{
    /// <summary>
    /// 
    /// </summary>
    public class ExternalTool
    {



        /// <summary>
        /// Gets
        /// </summary>
        public Form Window
        {
            get;
            private set;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="form_"></param>
        public ExternalTool(Form form_)
        {
            if (form_ == null)
            {
                throw new ArgumentNullException("ExternalTool() : Form is null");
            }

            Window = form_;
        }





    }
}
