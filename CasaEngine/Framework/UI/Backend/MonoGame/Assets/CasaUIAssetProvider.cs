using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Text;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Assets;

public sealed class CasaUIAssetProvider : IUIAssetProvider
{
    public ContentManager Content { get; }
    public FontManager FontManager { get; }

    public CasaUIAssetProvider(ContentManager content, FontManager fontManager)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fontManager);
        Content = content;
        FontManager = fontManager;
    }

    public IUIImageResource LoadImage(string assetName)
        => new CasaMonoGameImageResource(LoadTexture(assetName));

    public bool TryLoadImage(string assetName, out IUIImageResource image)
    {
        if (TryLoadTexture(assetName, out Texture2D texture))
        {
            image = new CasaMonoGameImageResource(texture);
            return true;
        }

        image = null!;
        return false;
    }

    public Texture2D LoadTexture(string assetName)
        => Content.Load<Texture2D>(assetName);

    public bool TryLoadTexture(string assetName, out Texture2D texture)
    {
        try
        {
            texture = LoadTexture(assetName);
            return true;
        }
        catch
        {
            texture = null!;
            return false;
        }
    }
}