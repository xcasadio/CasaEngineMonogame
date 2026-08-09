using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.TileMap;

public class AnimatedTile : Tile
{
    private readonly Texture2D _texture;
    private readonly Rectangle[] _frameSources;
    private readonly int[] _frameDurations;
    private int _currentFrameIndex;
    private float _elapsedFrameMilliseconds;

    public AnimatedTile(Texture2D texture, TileSetData tileSetData, AnimatedTileData tileData) : base(tileData)
    {
        _texture = texture;

        if (tileData.Frames.Count == 0)
        {
            _frameSources = new[] { tileData.Location };
            _frameDurations = new[] { 1 };
            return;
        }

        _frameSources = new Rectangle[tileData.Frames.Count];
        _frameDurations = new int[tileData.Frames.Count];
        for (var frameIndex = 0; frameIndex < tileData.Frames.Count; frameIndex++)
        {
            var frame = tileData.Frames[frameIndex];
            _frameDurations[frameIndex] = Math.Max(1, frame.DurationMilliseconds);
            _frameSources[frameIndex] = tileSetData.TryGetTileSourceRectangle(frame.TileId, out var sourceRectangle)
                ? sourceRectangle
                : tileData.Location;
        }
    }

    public int CurrentFrameIndex => _currentFrameIndex;

    public override void Update(float elapsedTime)
    {
        if (_frameSources.Length <= 1)
        {
            return;
        }

        _elapsedFrameMilliseconds += elapsedTime * 1000f;
        while (_elapsedFrameMilliseconds >= _frameDurations[_currentFrameIndex])
        {
            _elapsedFrameMilliseconds -= _frameDurations[_currentFrameIndex];
            _currentFrameIndex++;
            if (_currentFrameIndex >= _frameSources.Length)
            {
                _currentFrameIndex = 0;
            }
        }
    }

    public override void Draw(float x, float y, float z, Vector2 scale)
    {
        if (_texture == null || _frameSources.Length == 0)
        {
            return;
        }

        var sourceRectangle = _frameSources[_currentFrameIndex];
        var uvOffset = sourceRectangle;
        uvOffset.X = 0;
        uvOffset.Y = 0;
        base.Draw(_texture, sourceRectangle, x, y, z, uvOffset, scale);
    }

    public override void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags)
    {
        if (_texture == null || _frameSources.Length == 0)
        {
            return;
        }

        var sourceRectangle = _frameSources[_currentFrameIndex];
        var uvOffset = sourceRectangle;
        uvOffset.X = 0;
        uvOffset.Y = 0;
        base.Draw(_texture, sourceRectangle, x, y, z, uvOffset, scale, GetSpriteEffects(flags));
    }

    public override void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags, in Matrix worldTransform)
    {
        if (_texture == null || _frameSources.Length == 0)
        {
            return;
        }

        var sourceRectangle = _frameSources[_currentFrameIndex];
        var uvOffset = sourceRectangle;
        uvOffset.X = 0;
        uvOffset.Y = 0;
        base.Draw(_texture, sourceRectangle, x, y, z, uvOffset, scale, GetSpriteEffects(flags), in worldTransform);
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        if (_texture == null || _frameSources.Length == 0)
        {
            return;
        }

        base.Draw(_texture, _frameSources[_currentFrameIndex], x, y, z, uvOffset, scale);
    }

    private static SpriteEffects GetSpriteEffects(TileCellFlags flags)
    {
        var effects = SpriteEffects.None;
        if ((flags & TileCellFlags.FlipHorizontal) != 0)
        {
            effects |= SpriteEffects.FlipHorizontally;
        }

        if ((flags & TileCellFlags.FlipVertical) != 0)
        {
            effects |= SpriteEffects.FlipVertically;
        }

        return effects;
    }
}