
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework;
using CasaEngine.Game;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    /// <summary>
    /// 
    /// </summary>
    public partial class ScreenGadgetLabel
    {
        #region Fields

        public static int num = 0;

        #endregion

        #region Properties

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public ScreenGadgetLabel()
            : base("Label" + (num++))
        {
            Width = 100;
            Height = 80;
        }

        #endregion

        #region Methods

        #endregion
    }
}
