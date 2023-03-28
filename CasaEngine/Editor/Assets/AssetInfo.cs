namespace CasaEngine.Editor.Assets;

public struct AssetInfo
{
    private static int _freeId;

    public int Id;
    public readonly string Name;
    public readonly string FileName;
    public readonly AssetType Type;


    public AssetInfo(int id, string name, AssetType type, string fileName)
    {
        Id = id;
        Name = name;
        Type = type;
        FileName = fileName;
    }

    public override bool Equals(object obj)
    {
        if (obj is AssetInfo info)
        {
            return info.Name.Equals(Name)
                   && info.Type.Equals(Type)
                   && info.FileName.Equals(FileName);
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