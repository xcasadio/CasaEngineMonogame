using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Framework.Gameplay.Design.Event
{
    public abstract class Trigger
    {

        private readonly List<ITriggerEvent> _events = new();
        private bool _activated;
        private int _iterationMax = 1;
        private int _iteration;

        public bool Activated => _activated;

        public int IterationMax
        {
            get => _iterationMax;
            set => _iterationMax = value;
        }

#if EDITOR
        public
#else
        protected
#endif
        List<ITriggerEvent> Events
        {
            get { return _events; }
        }

        public Trigger()
        {
        }

        public Trigger(int iteration)
        {
            _iterationMax = iteration;
        }

        public Trigger(XmlElement el, SaveOption option)
        {
            Load(el, option);
        }

        public virtual void Load(XmlElement el, SaveOption option)
        {
            var loadedVersion = uint.Parse(el.Attributes["version"].Value);

            foreach (XmlNode node in el.SelectSingleNode("EventList").ChildNodes)
            {
                var assemblyFullName = node.Attributes["assemblyName"].Value;
                var typeFullName = node.Attributes["fullName"].Value;

                var t = Type.GetType(typeFullName);
                var ev = (ITriggerEvent)Activator.CreateInstance(t
#if EDITOR
                , new object[] { el, option }
#endif
                );

                _events.Add(ev);
            }
        }

        public virtual void Load(BinaryReader br, SaveOption option)
        {

        }

        public abstract bool Activate(float totalElapsedTime);

        public void AddEvent(ITriggerEvent @event)
        {
            _events.Add(@event);
        }

        public void Update(float totalElapsedTime)
        {
            if (Activate(totalElapsedTime) && (_iteration < _iterationMax || _iterationMax == -1))
            {
                foreach (var ev in _events.ToArray())
                {
                    ev.Do();
                }

                _activated = true;
                _iteration++;
            }
        }

        public void Reset()
        {
            _iteration = 0;
            _activated = false;
        }

#if EDITOR
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
#endif
    }
}
