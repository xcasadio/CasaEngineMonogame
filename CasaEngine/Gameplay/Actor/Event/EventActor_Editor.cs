using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Design.Event;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    /// <summary>
    /// 
    /// </summary>
    public partial class EventActor
    {
        #region Fields

        #endregion

        #region Properties

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "type", Enum.GetName(typeof(EventActorType), m_EventActorType));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bw_"></param>
        /// <param name="option_"></param>
        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)m_EventActorType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public virtual bool CompareTo(EventActor other_)
        {
            return m_EventActorType == other_.m_EventActorType;
        }

        #endregion
    }
}
