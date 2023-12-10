using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls.Skins;

public class SkinImage : SkinBase
{

    public Texture2D Resource;
    public string Asset;
    public string Addon;

    public SkinImage()
        : base()
    {
    }

    public SkinImage(SkinImage source)
        : base(source)
    {
        Resource = source.Resource;
        Asset = source.Asset;
    }

}