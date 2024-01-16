namespace TomShane.Neoforce.Controls.Skins;

public class SkinBase
{
    public string Name;

    public SkinBase(SkinBase? source = null)
    {
        if (source != null)
        {
            Name = source.Name;
        }
    }

}