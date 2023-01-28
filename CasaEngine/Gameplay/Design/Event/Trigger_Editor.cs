

using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Event
{
    /// <summary>
    /// 
    /// </summary>
    public abstract partial class Trigger
    {

        private static readonly uint m_Version = 1;







        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            XmlElement node;

            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());

            XmlElement eventListnode = el_.OwnerDocument.CreateElement("EventList");
            el_.AppendChild(eventListnode);

            foreach (TriggerEvent ev in m_Events)
            {
                node = el_.OwnerDocument.CreateElement("Event");
                eventListnode.AppendChild(node);

                Type t = ev.GetType();
                el_.OwnerDocument.AddAttribute(el_, "assemblyName", t.Assembly.FullName);
                //el_.OwnerDocument.AddAttribute(el_, "manifestModuleFullName", t.Assembly.ManifestModule.FullName);
                el_.OwnerDocument.AddAttribute(el_, "fullName", t.FullName);
                ev.Save(node, option_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
            bw_.Write(m_Events.Count);

            foreach (TriggerEvent ev in m_Events)
            {
                Type t = ev.GetType();
                bw_.Write(t.Assembly.FullName);
                bw_.Write(t.FullName);

                ev.Save(bw_, option_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="event_"></param>
        public void RemoveEvent(TriggerEvent event_)
        {
            m_Events.Remove(event_);
        }


    }
}
