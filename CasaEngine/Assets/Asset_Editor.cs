using System.Xml;
using CasaEngineCommon.Design;
using XNAFinalEngine.Helpers;
using System.ComponentModel;
using CasaEngine.Editor.Assets;

namespace CasaEngine.Asset
{
    public abstract partial class Asset
        : Disposable, ISaveLoad
    {

        [TypeConverter(typeof(AssetBuildParamCollectionConverter))]
        public AssetBuildParamCollection BuildParams;




        public virtual void Save(BinaryWriter br_, SaveOption option_)
        {

        }

        public virtual void Save(XmlElement el_, SaveOption option_)
        {

        }


    }
}
