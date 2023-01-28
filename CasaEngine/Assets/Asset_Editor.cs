using System.IO;
using System.Xml;
using CasaEngineCommon.Design;
using XNAFinalEngine.Helpers;
using System.ComponentModel;
using CasaEngine.Editor.Assets;

namespace CasaEngine.Asset
{
    /// <summary>
    /// 
    /// </summary>
    public abstract partial class Asset 
        : Disposable, ISaveLoad
    {

        [TypeConverter(typeof(AssetBuildParamCollectionConverter))]
        public AssetBuildParamCollection BuildParams;
        



        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public virtual void Save(BinaryWriter br_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Save(XmlElement el_, SaveOption option_)
        {

        }


    }
}
