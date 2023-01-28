
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

        public static int num = 0;





        /// <summary>
        /// 
        /// </summary>
        public ScreenGadgetLabel()
            : base("Label" + (num++))
        {
            Width = 100;
            Height = 80;
        }



    }
}
