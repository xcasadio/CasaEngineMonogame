using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce.Skins;

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