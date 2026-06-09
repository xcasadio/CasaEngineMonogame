using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using CasaEngine.Editor.ContentBrowser.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImage = System.Drawing.Image;
using DrawingRectangle = System.Drawing.Rectangle;

namespace CasaEngine.Editor.ContentBrowser.Services;

public readonly record struct ThumbnailCacheResult(Texture2D Texture, Point? SourceSize, bool IsLoaded);

public sealed class ThumbnailCache : IDisposable
{
    private enum CacheEntryStatus
    {
        Loading,
        Ready,
        Failed,
    }

    private sealed class CacheEntry
    {
        public string Path { get; }
        public CacheEntryStatus Status { get; set; }
        public Texture2D Texture { get; set; }
        public Point? SourceSize { get; set; }
        public long LastAccessSequence { get; set; }
        public long RequestId { get; }

        public CacheEntry(string path, CacheEntryStatus status, long lastAccessSequence, long requestId)
        {
            Path = path;
            Status = status;
            LastAccessSequence = lastAccessSequence;
            RequestId = requestId;
        }
    }

    private readonly record struct PendingThumbnailLoad(long RequestId, string Path, byte[] ImageBytes, Point? SourceSize, bool Succeeded);

    private static readonly ImageCodecInfo PngImageEncoder = ResolveImageEncoder(ImageFormat.Png);
    private static readonly ImageCodecInfo BmpImageEncoder = ResolveImageEncoder(ImageFormat.Bmp);

    private readonly GraphicsDevice _graphicsDevice;
    private readonly IReadOnlyList<IAssetThumbnailRenderer> _assetThumbnailRenderers;
    private readonly int _thumbnailSize;
    private readonly int _maxEntries;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<PendingThumbnailLoad> _completedLoads = new();
    private readonly object _syncRoot = new();
    private long _accessSequence;
    private long _nextRequestId;

    public event Action<string, Texture2D, Point> ThumbnailReady;

    public int EntryCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Count;
            }
        }
    }

    public ThumbnailCache(GraphicsDevice graphicsDevice, int thumbnailSize, int maxEntries = 500)
        : this(graphicsDevice, thumbnailSize, maxEntries, null)
    {
    }

    internal ThumbnailCache(GraphicsDevice graphicsDevice, int thumbnailSize, int maxEntries, IReadOnlyList<IAssetThumbnailRenderer> assetThumbnailRenderers)
    {
        _graphicsDevice = graphicsDevice;
        _assetThumbnailRenderers = assetThumbnailRenderers;
        _thumbnailSize = Math.Max(32, thumbnailSize);
        _maxEntries = Math.Max(1, maxEntries);
    }

    public ThumbnailCacheResult GetOrRequest(ContentItem item, Texture2D placeholder)
    {
        if (item == null)
        {
            return new ThumbnailCacheResult(placeholder, null, false);
        }

        if (!SupportsThumbnail(item))
        {
            return new ThumbnailCacheResult(placeholder, null, false);
        }

        var normalizedPath = NormalizePath(item.FullPath);
        CacheEntry entry;
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(normalizedPath, out entry))
            {
                entry.LastAccessSequence = ++_accessSequence;
                return new ThumbnailCacheResult(entry.Texture ?? placeholder, entry.SourceSize, entry.Status == CacheEntryStatus.Ready && entry.Texture != null);
            }

            var requestId = ++_nextRequestId;
            IAssetThumbnailRenderer assetThumbnailRenderer = ResolveAssetThumbnailRenderer(normalizedPath);
            bool usesAssetRenderer = assetThumbnailRenderer != null;
            var initialStatus = _graphicsDevice == null && !usesAssetRenderer ? CacheEntryStatus.Failed : CacheEntryStatus.Loading;
            entry = new CacheEntry(normalizedPath, initialStatus, ++_accessSequence, requestId);
            _entries[normalizedPath] = entry;
            if (usesAssetRenderer)
            {
                assetThumbnailRenderer!.Enqueue(normalizedPath, requestId);
            }
            else if (_graphicsDevice != null)
            {
                _ = Task.Run(() => LoadThumbnailAsync(normalizedPath, requestId));
            }
        }

        TrimToBudget();

        return new ThumbnailCacheResult(placeholder, null, false);
    }

    public bool TryGetCached(string path, out ThumbnailCacheResult result)
    {
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(NormalizePath(path), out var entry))
            {
                entry.LastAccessSequence = ++_accessSequence;
                result = new ThumbnailCacheResult(entry.Texture, entry.SourceSize, entry.Status == CacheEntryStatus.Ready && entry.Texture != null);
                return true;
            }
        }

        result = default;
        return false;
    }

    public void Update(int maxCreatesPerTick = 4)
    {
        if (_assetThumbnailRenderers != null)
        {
            for (int rendererIndex = 0; rendererIndex < _assetThumbnailRenderers.Count; rendererIndex++)
            {
                var assetThumbnailRenderer = _assetThumbnailRenderers[rendererIndex];
                assetThumbnailRenderer.Update();
                while (assetThumbnailRenderer.TryDequeueCompleted(out AssetThumbnailRenderResult renderedAssetThumbnail))
                {
                    lock (_syncRoot)
                    {
                        _completedLoads.Enqueue(new PendingThumbnailLoad(
                            renderedAssetThumbnail.RequestId,
                            renderedAssetThumbnail.Path,
                            renderedAssetThumbnail.ImageBytes,
                            renderedAssetThumbnail.SourceSize,
                            renderedAssetThumbnail.Succeeded));
                    }
                }
            }
        }

        for (var index = 0; index < maxCreatesPerTick; index++)
        {
            PendingThumbnailLoad pendingLoad;
            lock (_syncRoot)
            {
                if (_completedLoads.Count == 0)
                {
                    break;
                }

                pendingLoad = _completedLoads.Dequeue();
            }

            ApplyPendingLoad(pendingLoad);
        }
    }

    public void Invalidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        CacheEntry removedEntry = null;
        lock (_syncRoot)
        {
            if (_entries.Remove(NormalizePath(path), out var entry))
            {
                removedEntry = entry;
            }
        }

        removedEntry?.Texture?.Dispose();
    }

    public void InvalidateAll()
    {
        List<Texture2D> texturesToDispose = new();
        lock (_syncRoot)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Texture != null)
                {
                    texturesToDispose.Add(entry.Texture);
                }
            }

            _entries.Clear();
            _completedLoads.Clear();
        }

        foreach (var texture in texturesToDispose)
        {
            texture.Dispose();
        }
    }

    public void Dispose()
    {
        InvalidateAll();
        if (_assetThumbnailRenderers == null)
        {
            return;
        }

        for (int rendererIndex = 0; rendererIndex < _assetThumbnailRenderers.Count; rendererIndex++)
        {
            _assetThumbnailRenderers[rendererIndex].Dispose();
        }
    }

    public static bool SupportsThumbnail(ContentItem item)
        => item != null
        && !item.IsDirectory
        && (item.Type == ContentItemType.Texture || item.Type == ContentItemType.Particle || item.Type == ContentItemType.Sprite)
        && File.Exists(item.FullPath);

    private async Task LoadThumbnailAsync(string path, long requestId)
    {
        PendingThumbnailLoad completedLoad;
        try
        {
            completedLoad = await Task.Run(() => LoadThumbnail(path, requestId)).ConfigureAwait(false);
        }
        catch
        {
            completedLoad = new PendingThumbnailLoad(requestId, path, null, null, false);
        }

        lock (_syncRoot)
        {
            _completedLoads.Enqueue(completedLoad);
        }
    }

    private PendingThumbnailLoad LoadThumbnail(string path, long requestId)
        => LoadImageThumbnail(path, requestId);

    private PendingThumbnailLoad LoadImageThumbnail(string path, long requestId)
    {
        using var imageStream = File.OpenRead(path);
        using var image = DrawingImage.FromStream(imageStream, false, false);
        var sourceSize = new Point(image.Width, image.Height);
        using var bitmap = new DrawingBitmap(_thumbnailSize, _thumbnailSize);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var targetBounds = GetContainedRectangle(image.Width, image.Height, _thumbnailSize, _thumbnailSize);
        graphics.DrawImage(image, targetBounds);

        return new PendingThumbnailLoad(requestId, path, EncodeBitmapImageBytes(bitmap), sourceSize, true);
    }

    private static byte[] EncodeBitmapImageBytes(DrawingBitmap bitmap)
    {
        using var output = new MemoryStream();
        if (PngImageEncoder != null)
        {
            bitmap.Save(output, PngImageEncoder, null);
            return output.ToArray();
        }

        if (BmpImageEncoder != null)
        {
            bitmap.Save(output, BmpImageEncoder, null);
            return output.ToArray();
        }

        throw new InvalidOperationException("No image encoder is available for thumbnail generation.");
    }

    private static ImageCodecInfo ResolveImageEncoder(ImageFormat format)
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        for (var index = 0; index < encoders.Length; index++)
        {
            if (encoders[index].FormatID == format.Guid)
            {
                return encoders[index];
            }
        }

        return null;
    }

    private IAssetThumbnailRenderer ResolveAssetThumbnailRenderer(string path)
    {
        if (_assetThumbnailRenderers == null)
        {
            return null;
        }

        for (int rendererIndex = 0; rendererIndex < _assetThumbnailRenderers.Count; rendererIndex++)
        {
            var assetThumbnailRenderer = _assetThumbnailRenderers[rendererIndex];
            if (assetThumbnailRenderer.CanRender(path))
            {
                return assetThumbnailRenderer;
            }
        }

        return null;
    }

    private void ApplyPendingLoad(PendingThumbnailLoad pendingLoad)
    {
        Texture2D createdTexture = null;
        try
        {
            if (_graphicsDevice != null && pendingLoad.Succeeded && pendingLoad.ImageBytes != null)
            {
                using var stream = new MemoryStream(pendingLoad.ImageBytes, writable: false);
                createdTexture = Texture2D.FromStream(_graphicsDevice, stream);
            }

            CacheEntry existingEntry;
            lock (_syncRoot)
            {
                if (!_entries.TryGetValue(pendingLoad.Path, out existingEntry)
                    || existingEntry.RequestId != pendingLoad.RequestId)
                {
                    createdTexture?.Dispose();
                    return;
                }

                existingEntry.Texture?.Dispose();
                existingEntry.Texture = createdTexture;
                existingEntry.SourceSize = pendingLoad.SourceSize;
                existingEntry.Status = createdTexture != null ? CacheEntryStatus.Ready : pendingLoad.Succeeded ? CacheEntryStatus.Loading : CacheEntryStatus.Failed;
                existingEntry.LastAccessSequence = ++_accessSequence;
            }

            if (createdTexture != null && pendingLoad.SourceSize.HasValue)
            {
                ThumbnailReady?.Invoke(pendingLoad.Path, createdTexture, pendingLoad.SourceSize.Value);
            }
        }
        finally
        {
            TrimToBudget();
        }
    }

    private void TrimToBudget()
    {
        List<Texture2D> texturesToDispose = new();
        lock (_syncRoot)
        {
            while (_entries.Count > _maxEntries)
            {
                CacheEntry leastRecentlyUsed = null;
                foreach (var entry in _entries.Values)
                {
                    if (leastRecentlyUsed == null || entry.LastAccessSequence < leastRecentlyUsed.LastAccessSequence)
                    {
                        leastRecentlyUsed = entry;
                    }
                }

                if (leastRecentlyUsed == null)
                {
                    break;
                }

                _entries.Remove(leastRecentlyUsed.Path);
                if (leastRecentlyUsed.Texture != null)
                {
                    texturesToDispose.Add(leastRecentlyUsed.Texture);
                }
            }
        }

        foreach (var texture in texturesToDispose)
        {
            texture.Dispose();
        }
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static DrawingRectangle GetContainedRectangle(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new DrawingRectangle(0, 0, targetWidth, targetHeight);
        }

        var scale = Math.Min(targetWidth / (float)sourceWidth, targetHeight / (float)sourceHeight);
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        var x = (targetWidth - width) / 2;
        var y = (targetHeight - height) / 2;
        return new DrawingRectangle(x, y, width, height);
    }
}