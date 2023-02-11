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
        static private readonly int Version = 1;

        public virtual void Save(XmlElement el, SaveOption option)
        {
            XmlElement rootNode = el.OwnerDocument.CreateElement("BaseObject");
            el.AppendChild(rootNode);
            el.OwnerDocument.AddAttribute(rootNode, "version", Version.ToString());
        }

        public virtual void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
        }

        public abstract bool CompareTo(BaseObject other);
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

        protected BaseObject(XmlElement el, SaveOption option)
        {
            Load(el, option);
        }



        public virtual void Load(XmlElement el, SaveOption option)
        {
            XmlNode rootNode = el.SelectSingleNode("BaseObject");
            int loadedVersion = int.Parse(rootNode.Attributes["version"].Value);
        }

        public virtual void Load(BinaryReader br, SaveOption option)
        {
            uint loadedVersion = br.ReadUInt32();
            //int id = br_.ReadInt32();
            //TODO id
        }

        protected virtual void CopyFrom(BaseObject ob)
        {
            this.Temporary = ob.Temporary;
        }



        public abstract BaseObject Clone();

    }
}
