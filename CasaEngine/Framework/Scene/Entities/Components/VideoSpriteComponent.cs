using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Video;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Draws a catalogued MDEC video (ADR-0089) as a sprite of the world (ADR-0091): its position is the centre of the
/// picture and its Z orders it against the other sprites, the sprites of a smaller Z being drawn over it.
/// <para/>
/// The film does not test nor write the depth buffer (<c>ignoresDepth</c>), so a sprite of a smaller Z, drawn after it,
/// blends with it instead of being rejected by the depth test. Sprites that must blend with each other keep a same Z.
/// <para/>
/// The entity is made invisible by <see cref="InitializeWithWorld"/>, whatever its <c>is_visible</c> says: the film is
/// shown by a cutscene action (<see cref="Entity.IsVisible"/>) and started by <see cref="Play"/>. It advances with the
/// time received by <see cref="Update"/> (the scaled game time) only while it plays. When it ends, the last picture
/// stays drawn as long as the entity is visible.
/// <para/>
/// JSON: <c>video</c> (required), the name or the id of a catalogued <c>mdecvideo</c> asset; <c>color</c> (optional), an
/// object <c>{ "r", "g", "b", "a" }</c> of integers from 0 to 255, white when absent.
/// </summary>
[DisplayName("Video Sprite")]
public class VideoSpriteComponent : SceneComponent, IDisposable
{
    private static readonly string MdecVideoAssetType = Constants.FileNameExtensions.MdecVideo.TrimStart('.');

    private AssetHandle<MdecVideoAsset> _assetHandle;
    private MdecVideoPlayer _player;
    private SpriteRendererComponent _spriteRendererComponent;
    private bool _isPlaying;
    private int _width;
    private int _height;

    /// <summary>Tint passed to the renderer, as is. White (no tint) by default.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Order among sprites of the same z (ADR-0092): at equal z the smaller value is drawn first, so the larger one is drawn
    /// over it. 0 by default, which keeps the plain draw calls. No effect on a sprite sorted by a sort key.
    /// </summary>
    public int DrawOrder { get; set; }

    /// <summary>Name or id of the catalogued <c>mdecvideo</c> asset. Resolved in <see cref="InitializeWithWorld"/>.</summary>
    public string VideoName { get; set; }

    /// <summary>True between <see cref="Play"/> and the end of the film or <see cref="Stop"/>.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>True once the film reached its end or failed. False while there is no player.</summary>
    public bool IsEnded => _player != null && _player.IsEnded;

    /// <summary>True once <see cref="InitializeWithWorld"/> created the player, until <see cref="Detach"/> or <see cref="Dispose"/>.</summary>
    internal bool HasPlayer => _player != null;

    /// <summary>
    /// Incremented by each <see cref="Play"/>. A caller that started the film keeps the value it read: when it differs,
    /// a later play owns the film and the earlier caller must leave it alone.
    /// </summary>
    internal int PlayGeneration { get; private set; }

    /// <summary>The texture the film is drawn from, null without a player.</summary>
    internal Texture2D Texture => _player?.Texture;

    /// <summary>Index of the picture in the texture, -1 without a player.</summary>
    internal int CurrentFrameIndex => _player?.CurrentFrameIndex ?? -1;

    public VideoSpriteComponent()
    {
    }

    public VideoSpriteComponent(VideoSpriteComponent other) : base(other)
    {
        VideoName = other.VideoName;
        Color = other.Color;
        DrawOrder = other.DrawOrder;
    }

    public override VideoSpriteComponent Clone()
    {
        return new VideoSpriteComponent(this);
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        Owner.IsVisible = false;
        _spriteRendererComponent = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();

        ReleaseVideo();
        CreatePlayer(Owner.World.Game.AssetContentManager);
        IsBoundingBoxDirty = true;
    }

    /// <summary>
    /// Acquires the asset and creates the player. On a failure, whatever was acquired is released here (the loader does
    /// not detach an entity that failed) and an <see cref="InvalidDataException"/> naming the component, the field and
    /// the value is thrown.
    /// </summary>
    private void CreatePlayer(AssetContentManager assetContentManager)
    {
        if (string.IsNullOrEmpty(VideoName))
        {
            throw CreateFailure("no video is named.", null);
        }

        var assetInfo = Guid.TryParse(VideoName, out var assetId) ? AssetCatalog.Get(assetId) : AssetCatalog.Get(VideoName);
        if (assetInfo == null)
        {
            throw CreateFailure("it is not in the asset catalog.", null);
        }

        if (!string.Equals(assetInfo.AssetType, MdecVideoAssetType, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateFailure($"asset '{assetInfo.Name}' ({assetInfo.Id}) is a '{assetInfo.AssetType}' asset, not a video.", null);
        }

        AssetHandle<MdecVideoAsset> handle;
        try
        {
            handle = assetContentManager.Acquire<MdecVideoAsset>(assetInfo.Id);
        }
        catch (Exception exception)
        {
            throw CreateFailure($"the asset '{assetInfo.Name}' ({assetInfo.Id}) cannot be loaded: {exception.Message}", exception);
        }

        MdecVideoPlayer player;
        try
        {
            var graphicsDevice = assetContentManager.GraphicsDevice
                ?? throw new InvalidOperationException("the asset content manager has no graphics device.");
            player = new MdecVideoPlayer(graphicsDevice, handle.Asset);
        }
        catch (Exception exception)
        {
            handle.Dispose();
            throw CreateFailure($"the player of asset '{assetInfo.Name}' ({assetInfo.Id}) cannot be created: {exception.Message}", exception);
        }

        if (player.IsEnded)
        {
            // The constructor does not throw when the first picture cannot be decoded: it ends the video and logs it.
            player.Dispose();
            handle.Dispose();
            throw CreateFailure($"the first picture of asset '{assetInfo.Name}' ({assetInfo.Id}) cannot be decoded.", null);
        }

        _assetHandle = handle;
        _player = player;
        _width = handle.Asset.Width;
        _height = handle.Asset.Height;
    }

    private InvalidDataException CreateFailure(string reason, Exception innerException)
    {
        return new InvalidDataException(
            $"{nameof(VideoSpriteComponent)}: invalid 'video' '{VideoName}': {reason}", innerException);
    }

    /// <summary>Puts the film back to its first picture and starts it.</summary>
    /// <exception cref="InvalidOperationException">There is no player: <see cref="InitializeWithWorld"/> did not run.</exception>
    public void Play()
    {
        if (_player == null)
        {
            throw new InvalidOperationException($"{nameof(VideoSpriteComponent)}: the video '{VideoName}' is not loaded; the component is not initialized in a world.");
        }

        _player.Restart();
        _isPlaying = true;
        PlayGeneration++;
    }

    /// <summary>Stops the film where it is. The current picture stays drawn.</summary>
    public void Stop()
    {
        _isPlaying = false;
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (!_isPlaying || _player == null || !(elapsedTime > 0.0f))
        {
            return;
        }

        _player.Advance(new TimeSpan((long)Math.Round(elapsedTime * (double)TimeSpan.TicksPerSecond)));
        if (_player.IsEnded)
        {
            _isPlaying = false;
        }
    }

    public override void Draw(float elapsedTime)
    {
        if (_player == null || _spriteRendererComponent == null)
        {
            return;
        }

        var texture = _player.Texture;
        if (texture == null)
        {
            return;
        }

        var position = Position;
        var scale = Scale;
        if (DrawOrder != 0)
        {
            _spriteRendererComponent.DrawSprite(texture, texture.Bounds, new Point(texture.Width / 2, texture.Height / 2),
                new Vector2(position.X, position.Y), 0.0f, new Vector2(scale.X, scale.Y), Color, position.Z,
                SpriteEffects.None, ignoresDepth: true, DrawOrder);
            return;
        }

        _spriteRendererComponent.DrawSprite(texture, texture.Bounds, new Point(texture.Width / 2, texture.Height / 2),
            new Vector2(position.X, position.Y), 0.0f, new Vector2(scale.X, scale.Y), Color, position.Z,
            SpriteEffects.None, ignoresDepth: true);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (_player == null)
        {
            return base.GetBoundingBox();
        }

        return new BoundingBox(new Vector3(-_width / 2f, -_height / 2f, 0f), new Vector3(_width / 2f, _height / 2f, 0.1f))
            .Transform(WorldMatrixWithScale);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var videoToken = element["video"];
        if (videoToken == null || videoToken.Type != JTokenType.String || string.IsNullOrWhiteSpace(videoToken.Value<string>()))
        {
            throw new InvalidDataException(
                $"{nameof(VideoSpriteComponent)}: invalid 'video' {videoToken?.ToString(Newtonsoft.Json.Formatting.None) ?? "(missing)"}: expected the name or the id of a video asset.");
        }

        VideoName = videoToken.Value<string>();
        Color = element.ContainsKey("color") ? ReadColor(element["color"]) : Color.White;
        DrawOrder = element.ContainsKey("draw_order") ? ReadDrawOrder(element["draw_order"]) : 0;
    }

    private static int ReadDrawOrder(JToken token)
    {
        if (token.Type == JTokenType.Integer && token.Value<long>() is >= int.MinValue and <= int.MaxValue)
        {
            return token.Value<int>();
        }

        throw new InvalidDataException(
            $"{nameof(VideoSpriteComponent)}: invalid 'draw_order' {token.ToString(Newtonsoft.Json.Formatting.None)}: expected a JSON integer.");
    }

    private static Color ReadColor(JToken token)
    {
        if (token is not JObject node
            || !TryReadChannel(node, "r", out var r) || !TryReadChannel(node, "g", out var g)
            || !TryReadChannel(node, "b", out var b) || !TryReadChannel(node, "a", out var a))
        {
            throw new InvalidDataException(
                $"{nameof(VideoSpriteComponent)}: invalid 'color' {token.ToString(Newtonsoft.Json.Formatting.None)}: expected an object with the integer keys r, g, b, a, each from 0 to 255.");
        }

        return new Color(r, g, b, a);
    }

    private static bool TryReadChannel(JObject node, string key, out byte value)
    {
        value = 0;
        if (!node.TryGetValue(key, out var token) || token.Type != JTokenType.Integer)
        {
            return false;
        }

        var channel = token.Value<long>();
        if (channel is < 0 or > 255)
        {
            return false;
        }

        value = (byte)channel;
        return true;
    }

    public override void Detach()
    {
        ReleaseVideo();
        base.Detach();
    }

    /// <summary>Releases the player and the asset hold. Safe to call more than once.</summary>
    public void Dispose()
    {
        ReleaseVideo();
    }

    private void ReleaseVideo()
    {
        _isPlaying = false;
        _player?.Dispose();
        _player = null;
        _assetHandle?.Dispose();
        _assetHandle = null;
    }
}
