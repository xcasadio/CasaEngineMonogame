using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Services;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public sealed class ThumbnailCacheTests : IDisposable
{
    private readonly string _rootPath;

    public ThumbnailCacheTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_ThumbnailCache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void GetOrRequest_EvictsLeastRecentlyUsedEntry_WhenCapacityExceeded()
    {
        using var cache = new ThumbnailCache(null, 64, maxEntries: 2);
        var first = CreateTextureItem("first.png", Color.Red);
        var second = CreateTextureItem("second.png", Color.Green);
        var third = CreateTextureItem("third.png", Color.Blue);

        _ = cache.GetOrRequest(first, null);
        _ = cache.GetOrRequest(second, null);
        Assert.Equal(2, cache.EntryCount);

        Assert.True(cache.TryGetCached(first.FullPath, out _));
        _ = cache.GetOrRequest(third, null);

        Assert.Equal(2, cache.EntryCount);
        Assert.True(cache.TryGetCached(first.FullPath, out _));
        Assert.False(cache.TryGetCached(second.FullPath, out _));
        Assert.True(cache.TryGetCached(third.FullPath, out _));
    }

    [Fact]
    public void Invalidate_RemovesCachedEntry()
    {
        using var cache = new ThumbnailCache(null, 64, maxEntries: 2);
        var item = CreateTextureItem("single.png", Color.Orange);

        _ = cache.GetOrRequest(item, null);
        Assert.True(cache.TryGetCached(item.FullPath, out _));

        cache.Invalidate(item.FullPath);

        Assert.False(cache.TryGetCached(item.FullPath, out _));
        Assert.Equal(0, cache.EntryCount);
    }

    private ContentItem CreateTextureItem(string fileName, Color color)
    {
        var path = Path.Combine(_rootPath, fileName);
        using var bitmap = new Bitmap(8, 8);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(color);
        bitmap.Save(path, ImageFormat.Png);
        return new ContentItem(path, false);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}