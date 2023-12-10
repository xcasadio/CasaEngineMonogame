using FontStashSharp;

namespace TomShane.Neoforce.Controls.Skins;

public class SkinFont : SkinBase
{

    public SpriteFontBase Resource;
    public string Asset;
    public string Addon;

    public int Height
    {
        get
        {
            if (Resource != null)
            {
                return (int)Resource.MeasureString("AaYy").Y;
            }
            return 0;
        }
    }

    public SkinFont()
        : base()
    {
    }

    public SkinFont(SkinFont source)
        : base(source)
    {
        if (source != null)
        {
            Resource = source.Resource;
            Asset = source.Asset;
        }
    }

}