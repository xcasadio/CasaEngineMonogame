using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using CasaEngine.Editor.ContentBrowser.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImage = System.Drawing.Image;
using DrawingPen = System.Drawing.Pen;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSolidBrush = System.Drawing.SolidBrush;

namespace CasaEngine.Editor.ContentBrowser.Services;

public readonly record struct ThumbnailCacheResult(Texture2D? Texture, Point? SourceSize, bool IsLoaded);

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
        public Texture2D? Texture { get; set; }
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

    private readonly record struct PendingThumbnailLoad(long RequestId, string Path, byte[]? PngBytes, Point? SourceSize, bool Succeeded);

    private readonly record struct ParticleThumbnailDescriptor(
        string ShapeType,
        DrawingColor PrimaryColor,
        DrawingColor SecondaryColor,
        int EmitterCount);

    private readonly GraphicsDevice? _graphicsDevice;
    private readonly int _thumbnailSize;
    private readonly int _maxEntries;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<PendingThumbnailLoad> _completedLoads = new();
    private readonly object _syncRoot = new();
    private long _accessSequence;
    private long _nextRequestId;

    public event Action<string, Texture2D, Point>? ThumbnailReady;

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

    public ThumbnailCache(GraphicsDevice? graphicsDevice, int thumbnailSize, int maxEntries = 500)
    {
        _graphicsDevice = graphicsDevice;
        _thumbnailSize = Math.Max(32, thumbnailSize);
        _maxEntries = Math.Max(1, maxEntries);
    }

    public ThumbnailCacheResult GetOrRequest(ContentItem item, Texture2D? placeholder)
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
        CacheEntry? entry;
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(normalizedPath, out entry))
            {
                entry.LastAccessSequence = ++_accessSequence;
                return new ThumbnailCacheResult(entry.Texture ?? placeholder, entry.SourceSize, entry.Status == CacheEntryStatus.Ready && entry.Texture != null);
            }

            var requestId = ++_nextRequestId;
            var initialStatus = _graphicsDevice == null ? CacheEntryStatus.Failed : CacheEntryStatus.Loading;
            entry = new CacheEntry(normalizedPath, initialStatus, ++_accessSequence, requestId);
            _entries[normalizedPath] = entry;
            if (_graphicsDevice != null)
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

    public void Invalidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        CacheEntry? removedEntry = null;
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
    }

    public static bool SupportsThumbnail(ContentItem item)
        => item != null
        && !item.IsDirectory
        && (item.Type == ContentItemType.Texture || item.Type == ContentItemType.Particle)
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
    {
        if (IsParticleThumbnailPath(path))
        {
            return LoadParticleThumbnail(path, requestId);
        }

        return LoadImageThumbnail(path, requestId);
    }

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

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return new PendingThumbnailLoad(requestId, path, output.ToArray(), sourceSize, true);
    }

    private PendingThumbnailLoad LoadParticleThumbnail(string path, long requestId)
    {
        ParticleThumbnailDescriptor descriptor = ReadParticleThumbnailDescriptor(path);
        using var bitmap = new DrawingBitmap(_thumbnailSize, _thumbnailSize);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        DrawParticleThumbnail(graphics, descriptor, path, _thumbnailSize);

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return new PendingThumbnailLoad(requestId, path, output.ToArray(), new Point(_thumbnailSize, _thumbnailSize), true);
    }

    private static ParticleThumbnailDescriptor ReadParticleThumbnailDescriptor(string path)
    {
        try
        {
            var node = JObject.Parse(File.ReadAllText(path));
            var emitters = node["emitters"] as JArray;
            var emitter = emitters?.Count > 0 ? emitters[0] as JObject : null;
            string shapeType = emitter?["shape"]?["shape_type"]?.ToString() ?? "Point";
            var colorKeys = emitter?["initial"]?["start_color"]?["color_keys"] as JArray;
            DrawingColor primary = ReadParticleColor(colorKeys, 0, DrawingColor.FromArgb(255, 91, 211, 255));
            DrawingColor secondary = ReadParticleColor(colorKeys, Math.Max(0, (colorKeys?.Count ?? 1) - 1), DrawingColor.FromArgb(255, 255, 210, 91));
            return new ParticleThumbnailDescriptor(shapeType, primary, secondary, Math.Max(1, emitters?.Count ?? 1));
        }
        catch
        {
            return new ParticleThumbnailDescriptor(
                "Point",
                DrawingColor.FromArgb(255, 91, 211, 255),
                DrawingColor.FromArgb(255, 255, 210, 91),
                1);
        }
    }

    private static DrawingColor ReadParticleColor(JArray? colorKeys, int index, DrawingColor fallback)
    {
        if (colorKeys == null || index < 0 || index >= colorKeys.Count || colorKeys[index] is not JObject key)
        {
            return fallback;
        }

        var color = key["color"] as JObject;
        if (color == null)
        {
            return fallback;
        }

        int r = ClampColor(color["r"]?.Value<int>() ?? fallback.R);
        int g = ClampColor(color["g"]?.Value<int>() ?? fallback.G);
        int b = ClampColor(color["b"]?.Value<int>() ?? fallback.B);
        int a = ClampColor(color["a"]?.Value<int>() ?? fallback.A);
        return DrawingColor.FromArgb(a, r, g, b);
    }

    private static void DrawParticleThumbnail(
        DrawingGraphics graphics,
        ParticleThumbnailDescriptor descriptor,
        string path,
        int size)
    {
        var bounds = new DrawingRectangle(0, 0, size, size);
        using (var background = new LinearGradientBrush(
                   bounds,
                   DrawingColor.FromArgb(255, 20, 22, 28),
                   DrawingColor.FromArgb(255, 10, 12, 18),
                   LinearGradientMode.ForwardDiagonal))
        {
            graphics.FillRectangle(background, bounds);
        }

        DrawParticleDots(graphics, descriptor, path, size);
        DrawParticleShape(graphics, descriptor, size);

        using var border = new DrawingPen(DrawingColor.FromArgb(170, 110, 118, 130), 1.0f);
        graphics.DrawRectangle(border, 0, 0, size - 1, size - 1);
    }

    private static void DrawParticleDots(
        DrawingGraphics graphics,
        ParticleThumbnailDescriptor descriptor,
        string path,
        int size)
    {
        uint state = GetStableHash(path);
        int dotCount = Math.Min(30, 14 + descriptor.EmitterCount * 4);
        int margin = Math.Max(8, size / 9);
        int span = Math.Max(1, size - margin * 2);

        for (int index = 0; index < dotCount; index++)
        {
            state = state * 1664525u + 1013904223u;
            float x = margin + state % span;
            state = state * 1664525u + 1013904223u;
            float y = margin + state % span;
            state = state * 1664525u + 1013904223u;
            float radius = 2.0f + state % 5;
            float blend = (index % 5) / 4.0f;
            DrawingColor color = LerpColor(descriptor.PrimaryColor, descriptor.SecondaryColor, blend, 160);

            using var brush = new DrawingSolidBrush(color);
            graphics.FillEllipse(brush, x - radius, y - radius, radius * 2.0f, radius * 2.0f);
        }
    }

    private static void DrawParticleShape(DrawingGraphics graphics, ParticleThumbnailDescriptor descriptor, int size)
    {
        float inset = size * 0.24f;
        float shapeSize = size - inset * 2.0f;
        using var pen = new DrawingPen(DrawingColor.FromArgb(230, descriptor.PrimaryColor), Math.Max(2.0f, size / 34.0f));
        using var secondaryPen = new DrawingPen(DrawingColor.FromArgb(180, descriptor.SecondaryColor), Math.Max(1.5f, size / 48.0f));

        string shape = descriptor.ShapeType.ToLowerInvariant();
        if (shape == "circle")
        {
            graphics.DrawEllipse(pen, inset, inset, shapeSize, shapeSize);
            return;
        }

        if (shape == "sphere")
        {
            graphics.DrawEllipse(pen, inset, inset, shapeSize, shapeSize);
            graphics.DrawEllipse(secondaryPen, inset + shapeSize * 0.2f, inset, shapeSize * 0.6f, shapeSize);
            graphics.DrawLine(secondaryPen, inset, size * 0.5f, inset + shapeSize, size * 0.5f);
            return;
        }

        if (shape == "box")
        {
            float offset = shapeSize * 0.18f;
            graphics.DrawRectangle(pen, inset, inset + offset, shapeSize - offset, shapeSize - offset);
            graphics.DrawRectangle(secondaryPen, inset + offset, inset, shapeSize - offset, shapeSize - offset);
            graphics.DrawLine(secondaryPen, inset, inset + offset, inset + offset, inset);
            graphics.DrawLine(secondaryPen, inset + shapeSize - offset, inset + offset, inset + shapeSize, inset);
            graphics.DrawLine(secondaryPen, inset, inset + shapeSize, inset + offset, inset + shapeSize - offset);
            graphics.DrawLine(secondaryPen, inset + shapeSize - offset, inset + shapeSize, inset + shapeSize, inset + shapeSize - offset);
            return;
        }

        if (shape == "cone")
        {
            var points = new[]
            {
                new DrawingPointF(size * 0.5f, inset),
                new DrawingPointF(inset + shapeSize * 0.12f, inset + shapeSize),
                new DrawingPointF(inset + shapeSize * 0.88f, inset + shapeSize),
            };
            graphics.DrawPolygon(pen, points);
            graphics.DrawEllipse(secondaryPen, inset + shapeSize * 0.12f, inset + shapeSize * 0.86f, shapeSize * 0.76f, shapeSize * 0.28f);
            return;
        }

        graphics.DrawLine(pen, size * 0.5f, inset, size * 0.5f, inset + shapeSize);
        graphics.DrawLine(pen, inset, size * 0.5f, inset + shapeSize, size * 0.5f);
        graphics.DrawEllipse(secondaryPen, inset + shapeSize * 0.38f, inset + shapeSize * 0.38f, shapeSize * 0.24f, shapeSize * 0.24f);
    }

    private static DrawingColor LerpColor(DrawingColor left, DrawingColor right, float amount, int alpha)
    {
        int r = ClampColor((int)MathF.Round(left.R + (right.R - left.R) * amount));
        int g = ClampColor((int)MathF.Round(left.G + (right.G - left.G) * amount));
        int b = ClampColor((int)MathF.Round(left.B + (right.B - left.B) * amount));
        return DrawingColor.FromArgb(ClampColor(alpha), r, g, b);
    }

    private static int ClampColor(int value)
        => Math.Clamp(value, 0, 255);

    private static bool IsParticleThumbnailPath(string path)
        => string.Equals(Path.GetExtension(path), ".particle", StringComparison.OrdinalIgnoreCase);

    private static uint GetStableHash(string value)
    {
        uint hash = 2166136261u;
        for (int index = 0; index < value.Length; index++)
        {
            hash ^= value[index];
            hash *= 16777619u;
        }

        return hash == 0 ? 1u : hash;
    }

    private void ApplyPendingLoad(PendingThumbnailLoad pendingLoad)
    {
        Texture2D? createdTexture = null;
        try
        {
            if (_graphicsDevice != null && pendingLoad.Succeeded && pendingLoad.PngBytes != null)
            {
                using var stream = new MemoryStream(pendingLoad.PngBytes, writable: false);
                createdTexture = Texture2D.FromStream(_graphicsDevice, stream);
            }

            CacheEntry? existingEntry;
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
                CacheEntry? leastRecentlyUsed = null;
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