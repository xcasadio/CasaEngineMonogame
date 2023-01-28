using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.ComponentModel;
using System.IO;
using CasaEngineCommon.Extension;
using System.Xml;
using CasaEngine.AI;
using CasaEngine;
using CasaEngine.Gameplay.Design;
using CasaEngineCommon.Design;


namespace CasaEngine.Gameplay.Actor.Object
{
    /// <summary>
    /// All objects (game or editor) derives from it
    /// </summary>
    public abstract class BaseObject
        : BaseEntity, ISaveLoad, IActorCloneable
    {

#if EDITOR
        static private readonly int m_Version = 1;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_">le noeud qui va contenir les informations de l'objet</param>
        /// <param name="option_"></param>
        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            XmlElement rootNode = el_.OwnerDocument.CreateElement("BaseObject");
            el_.AppendChild(rootNode);
            el_.OwnerDocument.AddAttribute(rootNode, "version", m_Version.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_">le noeud qui va contenir les informations de l'objet</param>
        /// <param name="option_"></param>
        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        public abstract bool CompareTo(BaseObject other_);
#endif


        /// <summary>
        /// Gets if object is temporary (not saved in world)
        /// Just a copy of an other object
        /// </summary>
#if EDITOR
        [Category("Object"),
        ReadOnly(true)]
#endif
        public bool Temporary
        {
            get;
            internal set;
        }



        /// <summary>
        /// 
        /// </summary>
        protected BaseObject() { }

        /// <summary>
        /// 
        /// </summary>
        protected BaseObject(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlNode_"></param>
        public virtual void Load(XmlElement el_, SaveOption option_)
        {
            XmlNode rootNode = el_.SelectSingleNode("BaseObject");
            int loadedVersion = int.Parse(rootNode.Attributes["version"].Value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlNode_"></param>
        public virtual void Load(BinaryReader br_, SaveOption option_)
        {
            uint loadedVersion = br_.ReadUInt32();
            //int id = br_.ReadInt32();
            //TODO id
        }

        /// <summary>
		/// Copy
		/// </summary>
		/// <param name="ob_"></param>
		protected virtual void CopyFrom(BaseObject ob_)
        {
            this.Temporary = ob_.Temporary;
        }



        /// <summary>
        /// Copy
        /// </summary>
        /// <returns></returns>
        public abstract BaseObject Clone();

    }
}
