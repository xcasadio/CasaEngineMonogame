using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal sealed class CasaTextureCache
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<Color, SolidColorTexture> _solidColorTextures = new();
    private readonly Dictionary<int, Texture2D> _circleTextures = new();

    private const int MinimumCircleTextureRadius = 32;
    private const int MaximumCircleTextureRadius = 1024;

    public CasaTextureCache(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(spriteBatch);

        _graphicsDevice = graphicsDevice;
        spriteBatch.Disposing += (_, _) => DisposeCachedTextures();
    }

    public SolidColorTexture GetOrCreateSolidColorTexture(Color color)
    {
        if (!_solidColorTextures.TryGetValue(color, out SolidColorTexture result))
        {
            result = new SolidColorTexture(_graphicsDevice, color);
            _solidColorTextures.Add(color, result);
        }

        return result;
    }

    public Texture2D GetOrCreateWhiteCircleTexture(float desiredRadius, int? minimumRadius = null, int? maximumRadius = null)
    {
        maximumRadius = Math.Clamp(maximumRadius ?? GeneralUtils.NextPowerOf2(desiredRadius), MinimumCircleTextureRadius, MaximumCircleTextureRadius);
        minimumRadius = Math.Clamp(minimumRadius ?? (int)Math.Floor(desiredRadius), MinimumCircleTextureRadius, maximumRadius.Value);

        Texture2D bestMatch = null;
        float bestDifference = float.MaxValue;
        foreach (KeyValuePair<int, Texture2D> entry in _circleTextures)
        {
            if (entry.Value == null || entry.Value.IsDisposed)
            {
                continue;
            }

            if (entry.Key >= minimumRadius && entry.Key <= maximumRadius)
            {
                float difference = Math.Abs(desiredRadius - entry.Key);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestMatch = entry.Value;
                }
            }
        }

        if (bestMatch != null)
        {
            return bestMatch;
        }

        desiredRadius = Math.Min(desiredRadius, maximumRadius.Value);
        int actualRadius = Math.Clamp(GeneralUtils.NextPowerOf2(desiredRadius), minimumRadius.Value, maximumRadius.Value);
        Texture2D circle = CreateCircleTexture(actualRadius, Color.White);
        _circleTextures[actualRadius] = circle;
        return circle;
    }

    public void ClearDisposedCircleTextures()
    {
        List<int> invalidKeys = null;
        foreach (KeyValuePair<int, Texture2D> entry in _circleTextures)
        {
            if (entry.Value == null || entry.Value.IsDisposed)
            {
                invalidKeys ??= new List<int>();
                invalidKeys.Add(entry.Key);
            }
        }

        if (invalidKeys == null)
        {
            return;
        }

        foreach (int key in invalidKeys)
        {
            _circleTextures.Remove(key);
        }
    }

    private Texture2D CreateCircleTexture(int diameter, Color color)
    {
        int size = Math.Max(2, diameter);
        float radius = size / 2f;
        Vector2 center = new(radius, radius);
        Color[] data = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x, y);
                bool isInsideCircle = Vector2.DistanceSquared(point, center) <= radius * radius;
                data[(y * size) + x] = isInsideCircle ? color : Color.Transparent;
            }
        }

        Texture2D texture = new(_graphicsDevice, size, size);
        texture.SetData(data);
        return texture;
    }

    private void DisposeCachedTextures()
    {
        foreach (SolidColorTexture texture in _solidColorTextures.Values)
        {
            if (!texture.IsDisposed)
            {
                texture.Dispose();
            }
        }

        foreach (Texture2D texture in _circleTextures.Values)
        {
            if (texture != null && !texture.IsDisposed)
            {
                texture.Dispose();
            }
        }

        _solidColorTextures.Clear();
        _circleTextures.Clear();
    }
}