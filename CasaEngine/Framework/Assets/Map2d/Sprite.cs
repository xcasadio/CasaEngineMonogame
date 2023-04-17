using CasaEngine.Framework.Assets.Textures;

namespace CasaEngine.Framework.Assets.Map2d;

public class Sprite
{
    //TODO : remove and use AssetManager
    private static Dictionary<string, Texture> _textureCache = new();

    public Texture Texture { get; set; }
    public SpriteData SpriteData { get; set; }

    public Sprite(SpriteData spriteData)
    {
        if (_textureCache.TryGetValue(spriteData.FileName, out var texture))
        {
            Texture = texture;
        }
        else
        {
            throw new NotImplementedException();
            //auto* textureFile = Game::Instance().GetMediaManager().FindMedia(spriteData.GetAssetFileName().c_str(), true);
            //Texture = Texture::loadTexture(textureFile);
            //_textureCache.insert(std::make_pair(spriteData.GetAssetFileName(), texture));
        }

        SpriteData = spriteData;
    }
};