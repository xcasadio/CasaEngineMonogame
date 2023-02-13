using System.Xml;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;

namespace CasaEngine.Gameplay.Design.Event
{
    public abstract partial class Trigger
    {
        private static readonly uint Version = 1;

        public virtual void Save(XmlElement el, SaveOption option)
        {
            XmlElement node;

            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            var eventListnode = el.OwnerDocument.CreateElement("EventList");
            el.AppendChild(eventListnode);

            foreach (var ev in _events)
            {
                node = el.OwnerDocument.CreateElement("Event");
                eventListnode.AppendChild(node);

                var t = ev.GetType();
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

            foreach (var ev in _events)
            {
                var t = ev.GetType();
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
