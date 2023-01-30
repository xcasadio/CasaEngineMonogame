using System.ComponentModel;

namespace CasaEngine.Editor.Assets
{
    public struct AssetInfo
    {
        static private int FreeID = 0;

        public int ID;
        public string Name;
        public string FileName;
        public AssetType Type;

        [TypeConverter(typeof(AssetBuildParamCollectionConverter))]
        public AssetBuildParamCollection Params;

        public AssetInfo(int id_, string name_, AssetType type_, string fileName_)
        {
            ID = id_;
            Name = name_;
            Type = type_;
            FileName = fileName_;
            Params = new AssetBuildParamCollection();
        }

        public override bool Equals(object obj)
        {
            if (obj is AssetInfo)
            {
                AssetInfo a = (AssetInfo)obj;
                return a.Name.Equals(Name)
                    && a.Type.Equals(Type)
                    && a.FileName.Equals(FileName);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + Type.GetHashCode() + ID.GetHashCode();
        }

        public void SetID(int id_)
        {
            ID = id_;

            if (ID >= FreeID)
            {
                FreeID = ID + 1;
            }
        }

        public void GetNewID()
        {
            this.ID = ++FreeID;
        }
    }
}
