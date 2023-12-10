namespace TomShane.Neoforce.Controls.Skins;

public class SkinBase
{

    public string Name;
    public bool Archive;

    public SkinBase()
    {
        Archive = false;
    }

    public SkinBase(SkinBase source)
    {
        if (source != null)
        {
            Name = source.Name;
            Archive = source.Archive;
        }
    }

}