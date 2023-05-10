using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class AnimatedTile : Tile
{
    public Animation2d? Animation { get; set; }

    public AnimatedTile(Animation2d animation, AnimatedTileData tileData) : base(tileData)
    {
        Animation = animation;
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);
        Animation?.Initialize();
    }

    public override void Update(float elapsedTime)
    {
        Animation?.Update(elapsedTime);
    }

    public override void Draw(float x, float y, float z, Vector2 scale)
    {
        if (Animation == null)
        {
            return;
        }

        //TODO : load all sprite in initialization function
        Sprite sprite = null;//new Sprite(*Game::Instance().GetAssetManager().GetAsset<SpriteData>(_animation->CurrentFrame()));
        base.Draw(sprite, x, y, z, sprite.SpriteData.PositionInTexture, scale);
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        if (Animation == null)
        {
            return;
        }

        //TODO : load all sprite in initialization function
        Sprite sprite = null;//new Sprite(*Game::Instance().GetAssetManager().GetAsset<SpriteData>(_animation->CurrentFrame()));
        base.Draw(sprite, x, y, z, uvOffset, scale);
    }
}