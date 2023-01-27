using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

namespace CasaEngineCommon.Design
{
	/// <summary>
	/// 
	/// </summary>
	public enum SaveOption
	{
        /// <summary>
        /// Save editor and game data
        /// </summary>
		Editor,

        /// <summary>
        /// Save only game data
        /// </summary>
		Game
	}

	/// <summary>
	/// Save load interface
	/// </summary>
	public interface ISaveLoad
	{
#if EDITOR
        void Save(BinaryWriter bw_, SaveOption option_);
		void Save(XmlElement el_, SaveOption option_);
#endif
        void Load(BinaryReader br_, SaveOption option_);
		void Load(XmlElement el_, SaveOption option_);
	}
}
