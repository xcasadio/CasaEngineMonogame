
#region Using Directives

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

#endregion

namespace CasaEngine.Gameplay.Actor.Object
{
	/// <summary>
	/// All objects (game or editor) derives from it
	/// </summary>
	public abstract	
#if EDITOR
	partial		
#endif
	class BaseObject
        : BaseEntity, ISaveLoad, IActorCloneable
	{
		#region Fields

		#endregion

		#region Properties

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
 
		#endregion

		#region Constructor

		/// <summary>
		/// 
		/// </summary>
		protected BaseObject() {}

        /// <summary>
        /// 
        /// </summary>
        protected BaseObject(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }

        #endregion

        #region Method

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

		#endregion

		#region Abstract Methods

		/// <summary>
		/// Copy
		/// </summary>
		/// <returns></returns>
		public abstract BaseObject Clone();

		#endregion
	}
}
