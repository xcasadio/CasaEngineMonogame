using System.ComponentModel;
using CasaEngineCommon.Extension;
using System.Xml;
using CasaEngine.AI;
using CasaEngine.Gameplay.Design;
using CasaEngineCommon.Design;


namespace CasaEngine.Gameplay.Actor.Object
{
    public abstract class BaseObject
        : BaseEntity, ISaveLoad, IActorCloneable
    {

#if EDITOR
        static private readonly int m_Version = 1;

        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            XmlElement rootNode = el_.OwnerDocument.CreateElement("BaseObject");
            el_.AppendChild(rootNode);
            el_.OwnerDocument.AddAttribute(rootNode, "version", m_Version.ToString());
        }

        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
        }

        public abstract bool CompareTo(BaseObject other_);
#endif


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

        protected BaseObject(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }



        public virtual void Load(XmlElement el_, SaveOption option_)
        {
            XmlNode rootNode = el_.SelectSingleNode("BaseObject");
            int loadedVersion = int.Parse(rootNode.Attributes["version"].Value);
        }

        public virtual void Load(BinaryReader br_, SaveOption option_)
        {
            uint loadedVersion = br_.ReadUInt32();
            //int id = br_.ReadInt32();
            //TODO id
        }

        protected virtual void CopyFrom(BaseObject ob_)
        {
            this.Temporary = ob_.Temporary;
        }



        public abstract BaseObject Clone();

    }
}
