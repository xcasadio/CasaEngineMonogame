using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Event
{
    public abstract partial class Trigger
    {

        private static readonly uint Version = 1;







        public virtual void Save(XmlElement el, SaveOption option)
        {
            XmlElement node;

            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            XmlElement eventListnode = el.OwnerDocument.CreateElement("EventList");
            el.AppendChild(eventListnode);

            foreach (ITriggerEvent ev in _events)
            {
                node = el.OwnerDocument.CreateElement("Event");
                eventListnode.AppendChild(node);

                Type t = ev.GetType();
                el.OwnerDocument.AddAttribute(el, "assemblyName", t.Assembly.FullName);
                //el_.OwnerDocument.AddAttribute(el_, "manifestModuleFullName", t.Assembly.ManifestModule.FullName);
                el.OwnerDocument.AddAttribute(el, "fullName", t.FullName);
                ev.Save(node, option);
            }
        }

        public virtual void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(_events.Count);

            foreach (ITriggerEvent ev in _events)
            {
                Type t = ev.GetType();
                bw.Write(t.Assembly.FullName);
                bw.Write(t.FullName);

                ev.Save(bw, option);
            }
        }

        public void RemoveEvent(ITriggerEvent @event)
        {
            _events.Remove(@event);
        }


    }
}
