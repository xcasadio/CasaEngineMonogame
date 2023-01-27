using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Graphics2D;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;
using CasaEngine.FrontEnd.Screen.Gadget;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    /// <summary>
    /// 
    /// </summary>
    public partial class UIScreen
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Save(XmlElement el_, SaveOption opt_)
        {
            base.Save(el_, opt_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Save(BinaryWriter bw_, SaveOption opt_)
        {
            base.Save(bw_, opt_);
        }
    }
}
