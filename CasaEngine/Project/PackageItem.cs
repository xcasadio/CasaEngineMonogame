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

        public int Id
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



        protected PackageItem(Package package, int id, string path)
        {
            Package = package;
            Id = id;
            FullPath = path;
        }



        public abstract T LoadItem<T>();


#if EDITOR
        public void Save(BinaryWriter bw, SaveOption option)
        {

        }

        public void Save(XmlElement el, SaveOption option)
        {

        }
#endif
        public void Load(BinaryReader br, SaveOption option)
        {

        }
        public void Load(XmlElement el, SaveOption option)
        {

        }


    }
}
