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
        #region Fields

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public Form Window
        {
            get;
            private set;
        }

        #endregion

        #region Constructor

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

        #endregion

        #region Methods



        #endregion
    }
}
