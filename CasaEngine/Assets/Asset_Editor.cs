using System.ComponentModel;
using System.Xml;
using CasaEngine.Editor.Assets;
using CasaEngine.Helpers;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets
{
    public abstract partial class Asset
        : Disposable, ISaveLoad
    {

        [TypeConverter(typeof(AssetBuildParamCollectionConverter))]
        public AssetBuildParamCollection BuildParams;




        public virtual void Save(BinaryWriter br, SaveOption option)
        {

        }

        public virtual void Save(XmlElement el, SaveOption option)
        {

        }


    }
}
