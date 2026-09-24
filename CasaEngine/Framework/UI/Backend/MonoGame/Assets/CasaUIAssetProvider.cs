using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Configuration;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Text;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Assets;

public sealed class CasaUIAssetProvider : IUIAssetProvider, IDisposable
{
    private static readonly string SpriteAssetType = Constants.FileNameExtensions.Sprite.TrimStart('.');
    private static readonly string Animation2dAssetType = Constants.FileNameExtensions.Animation2d.TrimStart('.');

    private readonly AssetContentManager _assetContentManager;
    private readonly HashSet<string> _warnedNames = new();
    private readonly List<AssetHandle<SpriteData>> _heldSpriteData = new();
    private readonly List<Sprite> _heldSprites = new();

    /// <summary>One entry per distinct animation name resolved by <see cref="TryCreateAnimatedImage"/>: the
    /// composition is built once (<see cref="Animation2dCompositionAdapter.Create"/> allocates) and shared by
    /// every <see cref="CasaUIAnimatedImage"/> instance created for that name.</summary>
    private readonly Dictionary<string, AnimationCacheEntry> _animationCacheByName = new();

    /// <summary>One entry per distinct sprite id any cached animation's frames reference, shared across every
    /// animation and every per-image instance -- the same cache-per-sprite-id path as <see cref="TryResolveImage"/>,
    /// just keyed by id instead of by name.</summary>
    private readonly Dictionary<Guid, SpriteCacheEntry> _spriteCacheBySpriteId = new();

    /// <summary>Every per-image instance this provider has created, so <see cref="Dispose"/> can mark them
    /// disposed: MGUI has no element teardown (an <c>MGImage</c> whose animated source is discarded without a
    /// source change is never disposed by MGUI itself), so an instance that outlives its element is otherwise
    /// never told to stop.</summary>
    private readonly List<CasaUIAnimatedImage> _createdAnimatedImages = new();

    private bool _disposed;

    public ContentManager Content { get; }
    public FontManager FontManager { get; }

    /// <param name="assetContentManager">The game's asset manager, used to resolve <see cref="TryResolveImage"/>
    /// names that are catalogued sprite assets (ADR-0038, ADR-0016). Optional: when null, <see cref="TryResolveImage"/>
    /// resolves nothing, same as before this parameter existed.</param>
    public CasaUIAssetProvider(ContentManager content, FontManager fontManager, AssetContentManager assetContentManager = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fontManager);
        Content = content;
        FontManager = fontManager;
        _assetContentManager = assetContentManager;
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

    /// <summary>Resolves <paramref name="name"/> as a catalogued sprite asset id (a GUID string) or asset name
    /// (ADR-0038, "Images are named by asset"; MGUI ADR-0016). The sprite's sheet texture and its
    /// <see cref="SpriteData.PositionInTexture"/> are held through counted handles until this provider is
    /// disposed. Never throws: an unknown name, a non-sprite asset, a missing asset manager, or a load failure
    /// all return false, logging one warning per name.</summary>
    public bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)
    {
        image = null;
        sourceRect = null;

        if (_assetContentManager == null)
        {
            WarnOnce(name, "no AssetContentManager was provided to this UI asset provider");
            return false;
        }

        var assetInfo = Guid.TryParse(name, out var assetId) ? AssetCatalog.Get(assetId) : AssetCatalog.Get(name);
        if (assetInfo == null)
        {
            WarnOnce(name, "no catalogued asset matches this name or id");
            return false;
        }

        if (!string.Equals(assetInfo.AssetType, SpriteAssetType, StringComparison.OrdinalIgnoreCase))
        {
            // A 2D animation is a valid image source too: MGUI asks for a static image first, then for an animated
            // one (TryCreateAnimatedImage), which reports its own failures. Only other asset types are an error.
            if (!string.Equals(assetInfo.AssetType, Animation2dAssetType, StringComparison.OrdinalIgnoreCase))
            {
                WarnOnce(name, $"asset '{assetInfo.Name}' ({assetInfo.Id}) is a '{assetInfo.AssetType}' asset, neither a sprite nor a 2D animation");
            }

            return false;
        }

        AssetHandle<SpriteData> spriteDataHandle;
        try
        {
            spriteDataHandle = _assetContentManager.Acquire<SpriteData>(assetInfo.Id);
        }
        catch (Exception exception)
        {
            WarnOnce(name, $"failed to load sprite data for asset '{assetInfo.Name}' ({assetInfo.Id}): {exception.Message}");
            return false;
        }

        Sprite sprite;
        try
        {
            sprite = Sprite.Create(spriteDataHandle.Asset, _assetContentManager);
        }
        catch (Exception exception)
        {
            spriteDataHandle.Dispose();
            WarnOnce(name, $"failed to load the sheet texture for sprite asset '{assetInfo.Name}' ({assetInfo.Id}): {exception.Message}");
            return false;
        }

        _heldSpriteData.Add(spriteDataHandle);
        _heldSprites.Add(sprite);

        image = new CasaMonoGameImageResource(sprite.Texture.Resource);
        sourceRect = sprite.SpriteData.PositionInTexture;
        return true;
    }

    /// <summary>Resolves <paramref name="name"/> as a catalogued 2D animation asset (ADR-0038, MGUI ADR-0016,
    /// "Animated image sources") and returns a new, independent per-image player for it. The animation's
    /// composition data and every sprite its frames reference are resolved and held only once per name / per
    /// sprite id, in this provider (<see cref="_animationCacheByName"/>, <see cref="_spriteCacheBySpriteId"/>);
    /// the returned instance owns nothing but its own <see cref="Animation2dCompositionSampler"/>, so it never
    /// needs to be disposed for the shared assets to be released -- only this provider's own <see cref="Dispose"/>
    /// does that. Never throws: an unknown name, a non-animation asset, a missing asset manager, or a load
    /// failure all return false, logging one warning per name.</summary>
    public bool TryCreateAnimatedImage(string name, out IUIAnimatedImage animatedImage)
    {
        animatedImage = null;

        if (_assetContentManager == null)
        {
            WarnOnce(name, "no AssetContentManager was provided to this UI asset provider");
            return false;
        }

        if (!_animationCacheByName.TryGetValue(name, out var cacheEntry))
        {
            cacheEntry = TryCreateAnimationCacheEntry(name);
            if (cacheEntry == null)
            {
                return false;
            }

            _animationCacheByName.Add(name, cacheEntry);
        }

        var instance = new CasaUIAnimatedImage(this, cacheEntry.Composition);
        _createdAnimatedImages.Add(instance);
        animatedImage = instance;
        return true;
    }

    private AnimationCacheEntry TryCreateAnimationCacheEntry(string name)
    {
        var assetInfo = Guid.TryParse(name, out var assetId) ? AssetCatalog.Get(assetId) : AssetCatalog.Get(name);
        if (assetInfo == null)
        {
            WarnOnce(name, "no catalogued asset matches this name or id");
            return null;
        }

        if (!string.Equals(assetInfo.AssetType, Animation2dAssetType, StringComparison.OrdinalIgnoreCase))
        {
            // Not a 2D animation asset. This is not necessarily an error: TryResolveImage already handles (and
            // reports) the static-image case for this same name, so nothing further is logged here.
            return null;
        }

        AssetHandle<Animation2dData> animationHandle;
        try
        {
            animationHandle = _assetContentManager.Acquire<Animation2dData>(assetInfo.Id);
        }
        catch (Exception exception)
        {
            WarnOnce(name, $"failed to load animation data for asset '{assetInfo.Name}' ({assetInfo.Id}): {exception.Message}");
            return null;
        }

        Animation2dCompositionData composition;
        try
        {
            composition = Animation2dCompositionAdapter.Create(animationHandle.Asset);
        }
        catch (Exception exception)
        {
            animationHandle.Dispose();
            WarnOnce(name, $"failed to build the composition for animation asset '{assetInfo.Name}' ({assetInfo.Id}): {exception.Message}");
            return null;
        }

        CacheAnimationFrameSprites(composition, name);

        return new AnimationCacheEntry(animationHandle, composition);
    }

    /// <summary>Resolves and caches (by sprite id, once) every sprite <paramref name="composition"/>'s parts can
    /// show: each part's default sprite plus every value of every sprite track's keyframes. Called once per
    /// animation name, never on a per-image or per-frame path.</summary>
    private void CacheAnimationFrameSprites(Animation2dCompositionData composition, string animationName)
    {
        var parts = composition.Parts;
        for (var partIndex = 0; partIndex < parts.Count; partIndex++)
        {
            EnsureSpriteCached(parts[partIndex].DefaultSpriteId, animationName);
        }

        var tracks = composition.Tracks;
        for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            var track = tracks[trackIndex];
            if (track.Property != Animation2dTrackProperty.Sprite)
            {
                continue;
            }

            var spriteKeyframes = track.SpriteKeyframes;
            for (var keyframeIndex = 0; keyframeIndex < spriteKeyframes.Count; keyframeIndex++)
            {
                EnsureSpriteCached(spriteKeyframes[keyframeIndex].Value, animationName);
            }
        }
    }

    private void EnsureSpriteCached(Guid spriteId, string animationName)
    {
        if (spriteId == Guid.Empty || _spriteCacheBySpriteId.ContainsKey(spriteId))
        {
            return;
        }

        var spriteAssetInfo = AssetCatalog.Get(spriteId);
        if (spriteAssetInfo == null)
        {
            WarnOnce(animationName, $"animation frame sprite '{spriteId}' is not a catalogued asset");
            return;
        }

        AssetHandle<SpriteData> spriteDataHandle;
        try
        {
            spriteDataHandle = _assetContentManager.Acquire<SpriteData>(spriteAssetInfo.Id);
        }
        catch (Exception exception)
        {
            WarnOnce(animationName, $"failed to load animation frame sprite data for '{spriteAssetInfo.Name}' ({spriteAssetInfo.Id}): {exception.Message}");
            return;
        }

        Sprite sprite;
        try
        {
            sprite = Sprite.Create(spriteDataHandle.Asset, _assetContentManager);
        }
        catch (Exception exception)
        {
            spriteDataHandle.Dispose();
            WarnOnce(animationName, $"failed to load the sheet texture for animation frame sprite '{spriteAssetInfo.Name}' ({spriteAssetInfo.Id}): {exception.Message}");
            return;
        }

        var image = new CasaMonoGameImageResource(sprite.Texture.Resource);
        _spriteCacheBySpriteId.Add(spriteId, new SpriteCacheEntry(spriteDataHandle, sprite, image));
    }

    /// <summary>Looks up a cached animation frame sprite by id. Called from <see cref="CasaUIAnimatedImage"/>'s
    /// frame properties, so it must never allocate: a plain dictionary lookup.</summary>
    private bool TryGetCachedSprite(Guid spriteId, out SpriteCacheEntry entry)
        => _spriteCacheBySpriteId.TryGetValue(spriteId, out entry);

    private void WarnOnce(string name, string reason)
    {
        if (_warnedNames.Add(name))
        {
            Logs.WriteWarning($"CasaUIAssetProvider: cannot resolve UI image '{name}' ({reason}).");
        }
    }

    /// <summary>Gives back every sprite data and sheet texture handle held by <see cref="TryResolveImage"/> and
    /// <see cref="TryCreateAnimatedImage"/> (its cached animations and their frame sprites), and marks every
    /// per-image animation instance this provider created as disposed (MGUI itself never disposes one whose
    /// element was discarded without a source change -- see <see cref="_createdAnimatedImages"/>). Idempotent.
    /// After this call, the held assets are freed at the next <see cref="AssetContentManager.CollectUnreferenced"/>
    /// like any other unheld asset.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var sprite in _heldSprites)
        {
            sprite.Dispose();
        }
        _heldSprites.Clear();

        foreach (var handle in _heldSpriteData)
        {
            handle.Dispose();
        }
        _heldSpriteData.Clear();

        foreach (var instance in _createdAnimatedImages)
        {
            instance.Dispose();
        }
        _createdAnimatedImages.Clear();

        foreach (var entry in _spriteCacheBySpriteId.Values)
        {
            entry.Sprite.Dispose();
            entry.SpriteDataHandle.Dispose();
        }
        _spriteCacheBySpriteId.Clear();

        foreach (var entry in _animationCacheByName.Values)
        {
            entry.AnimationHandle.Dispose();
        }
        _animationCacheByName.Clear();
    }

    /// <summary>One cached, shared <see cref="Animation2dCompositionData"/> per animation name, and the handle
    /// that keeps its source <see cref="Animation2dData"/> alive until <see cref="Dispose"/>.</summary>
    private sealed class AnimationCacheEntry
    {
        public AssetHandle<Animation2dData> AnimationHandle { get; }
        public Animation2dCompositionData Composition { get; }

        public AnimationCacheEntry(AssetHandle<Animation2dData> animationHandle, Animation2dCompositionData composition)
        {
            AnimationHandle = animationHandle;
            Composition = composition;
        }
    }

    /// <summary>One cached, shared animation frame sprite: its sheet <see cref="Sprite"/> (and the handle that
    /// keeps its <see cref="SpriteData"/> alive) plus the <see cref="IUIImageResource"/> built once for it, so
    /// no per-image instance nor per-frame <see cref="CasaUIAnimatedImage"/> property ever allocates one.</summary>
    private sealed class SpriteCacheEntry
    {
        public AssetHandle<SpriteData> SpriteDataHandle { get; }
        public Sprite Sprite { get; }
        public CasaMonoGameImageResource Image { get; }

        public SpriteCacheEntry(AssetHandle<SpriteData> spriteDataHandle, Sprite sprite, CasaMonoGameImageResource image)
        {
            SpriteDataHandle = spriteDataHandle;
            Sprite = sprite;
            Image = image;
        }
    }

    /// <summary>A per-image player for one 2D animation composition (T3.2, ADR-0038, MGUI ADR-0016): owns only
    /// its own <see cref="Animation2dCompositionSampler"/>, driven by the UI clock through <see cref="Advance"/>.
    /// It owns no counted handle -- <see cref="CasaUIAssetProvider"/> holds the composition and every frame
    /// sprite this animation can show -- so it never needs to be disposed for those to be released; <see cref="Dispose"/>
    /// just marks this instance disposed, which is enough since it holds nothing else.<para/>
    /// Internal (not private) so <c>CasaEngine.Tests</c> can observe <see cref="IsDisposed"/> directly, since
    /// nothing in <see cref="IUIAnimatedImage"/> itself exposes it.</summary>
    internal sealed class CasaUIAnimatedImage : IUIAnimatedImage
    {
        private readonly CasaUIAssetProvider _provider;
        private readonly Animation2dCompositionSampler _sampler;

        internal bool IsDisposed { get; private set; }

        internal CasaUIAnimatedImage(CasaUIAssetProvider provider, Animation2dCompositionData composition)
        {
            _provider = provider;
            _sampler = new Animation2dCompositionSampler(composition);
        }

        public void Advance(TimeSpan elapsed)
        {
            _sampler.Update((float)elapsed.TotalSeconds);
        }

        public void Restart(TimeSpan startOffset)
        {
            _sampler.Reset();
            _sampler.Seek((float)startOffset.TotalSeconds);
        }

        public IUIImageResource CurrentImage => TryGetCurrentSpriteEntry(out var entry) ? entry.Image : null;

        public Rectangle? CurrentSourceRect => TryGetCurrentSpriteEntry(out var entry) ? entry.Sprite.SpriteData.PositionInTexture : null;

        public Point CurrentDrawOffset
        {
            get
            {
                if (!TryGetCurrentPart(out var part))
                {
                    return Point.Zero;
                }

                return new Point((int)MathF.Round(part.Position.X), (int)MathF.Round(part.Position.Y));
            }
        }

        /// <summary>The part in first draw-order position, i.e. <c>RuntimeState.Parts[RuntimeState.DrawPartIndices[0]]</c>.
        /// A UI animation clip has a single part; a clip with several parts uses the first one in draw order for
        /// its single image (documented limitation -- <see cref="MGImage"/> shows one frame, not a composed scene).</summary>
        private bool TryGetCurrentPart(out Animation2dPartRuntimeState part)
        {
            var runtimeState = _sampler.RuntimeState;
            if (runtimeState.PartCount == 0)
            {
                part = null;
                return false;
            }

            part = runtimeState.Parts[runtimeState.DrawPartIndices[0]];
            return true;
        }

        private bool TryGetCurrentSpriteEntry(out SpriteCacheEntry entry)
        {
            if (!TryGetCurrentPart(out var part))
            {
                entry = null;
                return false;
            }

            return _provider.TryGetCachedSprite(part.SpriteId, out entry);
        }

        /// <summary>Marks this instance disposed. It owns no counted handle (<see cref="CasaUIAssetProvider"/>
        /// owns the composition and its frame sprites), so nothing else to give back. Idempotent.</summary>
        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
