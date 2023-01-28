using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using CasaEngine.Audio;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public partial class PlaySoundEvent
    {







        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);
            el_.OwnerDocument.AddAttribute(el_, "asset", m_AssetName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);
            bw_.Write(m_AssetName);
        }

    }
}
