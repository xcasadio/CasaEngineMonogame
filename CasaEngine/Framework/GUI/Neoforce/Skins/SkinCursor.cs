namespace CasaEngine.Framework.GUI.Neoforce.Skins;

public class SkinCursor : SkinBase
{
    public Cursor Resource;

    public string Asset;
    public string Addon;

    public SkinCursor()
        : base()
    {
    }

    public SkinCursor(SkinCursor source)
        : base(source)
    {
        Resource = source.Resource;

        Asset = source.Asset;
    }

}