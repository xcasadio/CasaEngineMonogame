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

    [Fact]
    public void GetOrRequest_QueuesParticleThumbnailRender_WhenRendererIsProvided()
    {
        using var renderer = new FakeAssetThumbnailRenderer(path => path.EndsWith(".particle", StringComparison.OrdinalIgnoreCase));
        using var cache = new ThumbnailCache(null, 64, maxEntries: 2, new IAssetThumbnailRenderer[] { renderer });
        string path = CreateParticleFile("spark.particle");
        var item = new ContentItem(path, false);

        ThumbnailCacheResult result = cache.GetOrRequest(item, null);

        Assert.False(result.IsLoaded);
        Assert.Single(renderer.EnqueuedRequests);
        Assert.Equal(path, renderer.EnqueuedRequests[0].Path);
        Assert.Equal(1L, renderer.EnqueuedRequests[0].RequestId);
        Assert.Equal(1, cache.EntryCount);
    }

    [Fact]
    public void GetOrRequest_QueuesSpriteThumbnailRender_WhenRendererIsProvided()
    {
        using var renderer = new FakeAssetThumbnailRenderer(path => path.EndsWith(".sprite", StringComparison.OrdinalIgnoreCase));
        using var cache = new ThumbnailCache(null, 64, maxEntries: 2, new IAssetThumbnailRenderer[] { renderer });
        string path = CreateSpriteFile("hero.sprite");
        var item = new ContentItem(path, false);

        ThumbnailCacheResult result = cache.GetOrRequest(item, null);

        Assert.False(result.IsLoaded);
        Assert.Single(renderer.EnqueuedRequests);
        Assert.Equal(path, renderer.EnqueuedRequests[0].Path);
        Assert.Equal(1L, renderer.EnqueuedRequests[0].RequestId);
        Assert.Equal(1, cache.EntryCount);
    }

    private sealed class FakeAssetThumbnailRenderer : IAssetThumbnailRenderer
    {
        private readonly Func<string, bool> _canRender;

        public FakeAssetThumbnailRenderer(Func<string, bool> canRender)
        {
            _canRender = canRender;
        }

        public List<(string Path, long RequestId)> EnqueuedRequests { get; } = new();

        public bool CanRender(string path)
            => _canRender(path);

        public void Enqueue(string path, long requestId)
            => EnqueuedRequests.Add((path, requestId));

        public void Update()
        {
        }

        public bool TryDequeueCompleted(out AssetThumbnailRenderResult result)
        {
            result = default;
            return false;
        }

        public void Dispose()
        {
        }
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

    private string CreateParticleFile(string fileName)
    {
        var path = Path.Combine(_rootPath, fileName);
        File.WriteAllText(path, """
        {
            "id": "11111111-1111-1111-1111-111111111111",
            "name": "TestParticle",
            "emitters": [
                {
                    "name": "Emitter",
                    "enabled": true,
                    "duration": 1.0,
                    "looping": true,
                    "start_delay": 0.0,
                    "max_particles": 16,
                    "emission": {
                        "rate_over_time": 12.0,
                        "bursts": []
                    },
                    "shape": {
                        "shape_type": "Sphere"
                    },
                    "initial": {
                        "lifetime": {
                            "min": 0.5,
                            "max": 1.0
                        },
                        "speed": {
                            "min": 0.1,
                            "max": 0.3
                        },
                        "size": {
                            "min": { "x": 0.1, "y": 0.1 },
                            "max": { "x": 0.2, "y": 0.2 }
                        },
                        "start_color": {
                            "color_keys": [
                                {
                                    "time": 0.0,
                                    "color": {
                                        "r": 32,
                                        "g": 160,
                                        "b": 255,
                                        "a": 255
                                    }
                                }
                            ],
                            "alpha_keys": [
                                {
                                    "time": 0.0,
                                    "alpha": 1.0
                                }
                            ]
                        }
                    },
                    "simulation": {
                        "color_over_lifetime": {
                            "color_keys": [
                                {
                                    "time": 0.0,
                                    "color": {
                                        "r": 255,
                                        "g": 255,
                                        "b": 255,
                                        "a": 255
                                    }
                                }
                            ],
                            "alpha_keys": [
                                {
                                    "time": 0.0,
                                    "alpha": 1.0
                                }
                            ]
                        }
                    },
                    "renderer": {
                        "render_mode": "Billboard",
                        "texture_asset_id": "00000000-0000-0000-0000-000000000000",
                        "blend_mode": "Alpha",
                        "sort_mode": "Distance",
                        "depth_test": true,
                        "depth_write": false,
                        "render_queue": 3000,
                        "layer": 0,
                        "always_visible": false
                    }
                }
            ]
        }
        """);
        return path;
    }

    private string CreateSpriteFile(string fileName)
    {
        var path = Path.Combine(_rootPath, fileName);
        File.WriteAllText(path, """
        {
            "id": "22222222-2222-2222-2222-222222222222",
            "name": "HeroSprite",
            "sprite_sheet_asset_id": "33333333-3333-3333-3333-333333333333",
            "location": {
                "x": 0,
                "y": 0,
                "width": 32,
                "height": 48
            },
            "hotspot": {
                "x": 16,
                "y": 24
            },
            "collisions": [],
            "sockets": []
        }
        """);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}