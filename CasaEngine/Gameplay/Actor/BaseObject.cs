using System.ComponentModel;
using System.Xml;
using CasaEngine.Gameplay.Design;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;

namespace CasaEngine.Gameplay.Actor
{
    public abstract class BaseObject : ISaveLoad, IActorCloneable
    {
        public const int EntityNotRegistered = -1;

#if EDITOR
        private static readonly int Version = 1;

        [Category("Object"), ReadOnly(true)]
#endif
        public int Id { get; set; } = EntityNotRegistered;

#if EDITOR
        [Browsable(false)]
#endif
        public bool Remove { get; set; }

#if EDITOR
        [Category("Object"),
        ReadOnly(true)]
#endif
        public bool Temporary
        {
            get;
            internal set;
        }

        protected BaseObject() { }

        protected BaseObject(XmlElement el, SaveOption option)
        {
            Load(el, option);
        }

        public virtual void Load(XmlElement el, SaveOption option)
        {
            var rootNode = el.SelectSingleNode("BaseObject");
            var loadedVersion = int.Parse(rootNode.Attributes["version"].Value);
        }

        public virtual void Load(BinaryReader br, SaveOption option)
        {
            var loadedVersion = br.ReadUInt32();
            //int id = br_.ReadInt32();
            //TODO id
        }

#if EDITOR
        public virtual void Save(XmlElement el, SaveOption option)
        {
            var rootNode = el.OwnerDocument.CreateElement("BaseObject");
            el.AppendChild(rootNode);
            el.OwnerDocument.AddAttribute(rootNode, "version", Version.ToString());
        }

        public virtual void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
        }

        public abstract bool CompareTo(BaseObject other);
#endif

        protected virtual void Destroy() { }

        protected virtual void CopyFrom(BaseObject ob)
        {
            Temporary = ob.Temporary;
        }

        public abstract BaseObject Clone();
    }
}
