using CasaEngineCommon.Design;
using System.Xml;

namespace CasaEngine.Project
{
    public abstract class PackageItem
        : ISaveLoad
    {



        public Package Package
        {
            get;
            private set;
        }

        public int ID
        {
            get;
            private set;
        }

        public string FullPath
        {
            get;
            private set;
        }

        public string DirectoryPath => System.IO.Path.GetDirectoryName(FullPath);

        public string Name => System.IO.Path.GetFileName(FullPath);

        public List<string> Tags { get; internal set; }

        public DateTime DateTime { get; internal set; }



        protected PackageItem(Package package_, int id_, string path_)
        {
            Package = package_;
            ID = id_;
            FullPath = path_;
        }



        public abstract T LoadItem<T>();


#if EDITOR
        public void Save(BinaryWriter bw_, SaveOption option_)
        {

        }

        public void Save(XmlElement el_, SaveOption option_)
        {

        }
#endif
        public void Load(BinaryReader br_, SaveOption option_)
        {

        }
        public void Load(XmlElement el_, SaveOption option_)
        {

        }


    }
}
