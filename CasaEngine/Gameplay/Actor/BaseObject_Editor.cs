
#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Extension;
using Microsoft.Xna.Framework;
using CasaEngine;
using CasaEngineCommon.Design;


#endregion

namespace CasaEngine.Gameplay.Actor.Object
{
	/// <summary>
	/// All objects (game or editor) derives from it
	/// </summary>
	public abstract	partial class BaseObject
		: ISaveLoad
	{
		#region Fields

        static private readonly int m_Version = 1;

		#endregion // Fields

		#region Properties

		#endregion // Properties

		#region Constructor

		#endregion // Constructor

		#region Methods

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

        #endregion // Methods
    }
}
