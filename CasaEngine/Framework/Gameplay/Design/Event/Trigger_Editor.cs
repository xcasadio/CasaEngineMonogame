using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Framework.Gameplay.Design.Event
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

        public void RemoveEvent(ITriggerEvent @event)
        {
            _events.Remove(@event);
        }
    }
}
