using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Editor.Assets;

namespace CasaEngine.Framework.Assets
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
