namespace CasaEngine.Framework.GUI.Neoforce.Skins;

public class SkinAttribute : SkinBase
{

    public string Value;

    public SkinAttribute()
        : base()
    {
    }

    public SkinAttribute(SkinAttribute source)
        : base(source)
    {
        Value = source.Value;
    }

}