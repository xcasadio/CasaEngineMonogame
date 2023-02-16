using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public struct AssetInfo
    {
        private static int _freeId;

        public int Id;
        public string Name;
        public string FileName;
        public AssetType Type;

        [TypeConverter(typeof(AssetBuildParamCollectionConverter))]
        public AssetBuildParamCollection Params;

        public AssetInfo(int id, string name, AssetType type, string fileName)
        {
            Id = id;
            Name = name;
            Type = type;
            FileName = fileName;
            Params = new AssetBuildParamCollection();
        }

        public override bool Equals(object obj)
        {
            if (obj is AssetInfo)
            {
                var a = (AssetInfo)obj;
                return a.Name.Equals(Name)
                    && a.Type.Equals(Type)
                    && a.FileName.Equals(FileName);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + Type.GetHashCode() + Id.GetHashCode();
        }

        public void SetId(int id)
        {
            Id = id;

            if (Id >= _freeId)
            {
                _freeId = Id + 1;
            }
        }

        public void GetNewId()
        {
            Id = ++_freeId;
        }
    }
}
