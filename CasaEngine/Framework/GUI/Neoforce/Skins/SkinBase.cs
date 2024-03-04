namespace CasaEngine.Framework.GUI.Neoforce.Skins;

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