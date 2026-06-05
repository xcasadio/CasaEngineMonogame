using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingRectangle = System.Drawing.Rectangle;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace CasaEngine.Editor.ContentBrowser.Services;

internal sealed class SpriteSceneThumbnailRenderer : IAssetThumbnailRenderer
{
    private sealed class PendingThumbnailRequest
    {
        public PendingThumbnailRequest(long requestId, string path)
        {
            RequestId = requestId;
            Path = path;
        }

        public long RequestId { get; }

        public string Path { get; }
    }

    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly int _thumbnailSize;
    private readonly PreviewWorldDriver _previewWorldDriver;
    private readonly Queue<PendingThumbnailRequest> _pendingRequests = new();
    private readonly Queue<AssetThumbnailRenderResult> _completedResults = new();

    private RenderTargetSurface? _surface;
    private RenderView? _renderView;
    private Entity? _previewEntity;
    private StaticSpriteComponent? _previewSpriteComponent;
    private Entity? _cameraEntity;
    private CameraLookAtComponent? _camera;
    private PendingThumbnailRequest? _activeRequest;
    private bool _disposed;

    public SpriteSceneThumbnailRenderer(GraphicsDevice graphicsDevice, int thumbnailSize, HostedEditorGameAdapter editorRuntime)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(editorRuntime);

        _graphicsDevice = graphicsDevice;
        _thumbnailSize = Math.Max(32, thumbnailSize);
        _editorRuntime = editorRuntime;
        _previewWorldDriver = new PreviewWorldDriver(editorRuntime, new PreviewWorldDriverOptions
        {
            WorldName = "SpriteThumbnailPreviewWorld",
            UpdateMode = PreviewWorldUpdateMode.Manual,
        });
    }

    public bool CanRender(string path)
        => string.Equals(Path.GetExtension(path), Constants.FileNameExtensions.Sprite, StringComparison.OrdinalIgnoreCase);

    public void Enqueue(string path, long requestId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _pendingRequests.Enqueue(new PendingThumbnailRequest(requestId, path));
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        if (_activeRequest != null)
        {
            if (!TryCompleteActiveRequest())
            {
                return;
            }

            _activeRequest = null;
        }

        if (_pendingRequests.Count > 0)
        {
            StartNextRequest(_pendingRequests.Dequeue());
        }
    }

    public bool TryDequeueCompleted(out AssetThumbnailRenderResult result)
    {
        if (_completedResults.Count > 0)
        {
            result = _completedResults.Dequeue();
            return true;
        }

        result = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingRequests.Clear();
        _completedResults.Clear();
        _activeRequest = null;

        if (_renderView != null)
        {
            _editorRuntime.GameManager.ViewManager.Remove(_renderView);
            _renderView = null;
        }

        _surface?.Dispose();
        _surface = null;
        _previewWorldDriver.Dispose();
        _previewEntity = null;
        _previewSpriteComponent = null;
        _camera = null;
        _cameraEntity = null;
    }

    private void StartNextRequest(PendingThumbnailRequest request)
    {
        EnsureRenderViewCreated();

        if (!SpriteAssetInspectorPanel.TryLoadAsset(request.Path, out var spriteAsset)
            || _previewSpriteComponent == null
            || _previewWorldDriver.World == null)
        {
            _completedResults.Enqueue(new AssetThumbnailRenderResult(request.RequestId, request.Path, null, null, false));
            return;
        }

        CacheSpriteAsset(spriteAsset);
        Guid spriteAssetId = spriteAsset.AssetId != Guid.Empty ? spriteAsset.AssetId : spriteAsset.Id;
        _previewSpriteComponent.SpriteAssetId = spriteAssetId;
        _previewSpriteComponent.ReloadSpriteAsset(spriteAssetId, spriteAsset);
        _previewWorldDriver.RefreshNow();
        ConfigureCamera(spriteAsset);

        if (_renderView == null || _surface?.RenderTarget == null)
        {
            _completedResults.Enqueue(new AssetThumbnailRenderResult(request.RequestId, request.Path, null, null, false));
            return;
        }

        _renderView.RenderStats.RenderedThisFrame = false;
        _renderView.Invalidate();
        _activeRequest = request;
    }

    private bool TryCompleteActiveRequest()
    {
        if (_activeRequest == null)
        {
            return true;
        }

        if (_renderView == null || _surface?.RenderTarget == null)
        {
            _completedResults.Enqueue(new AssetThumbnailRenderResult(_activeRequest.RequestId, _activeRequest.Path, null, null, false));
            return true;
        }

        if (!_renderView.RenderStats.RenderedThisFrame)
        {
            return false;
        }

        try
        {
            using var output = new MemoryStream();
            RenderTarget2D renderTarget = _surface.RenderTarget;
            renderTarget.SaveAsPng(output, renderTarget.Width, renderTarget.Height);
            byte[] croppedBytes = CropTransparentPadding(output.ToArray(), out XnaPoint sourceSize);
            _completedResults.Enqueue(new AssetThumbnailRenderResult(
                _activeRequest.RequestId,
                _activeRequest.Path,
                croppedBytes,
                sourceSize,
                true));
        }
        catch
        {
            _completedResults.Enqueue(new AssetThumbnailRenderResult(_activeRequest.RequestId, _activeRequest.Path, null, null, false));
        }
        finally
        {
            _renderView.RenderStats.RenderedThisFrame = false;
        }

        return true;
    }

    private void EnsurePreviewSceneCreated()
    {
        if (_previewWorldDriver.World != null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world =>
        {
            _previewEntity = new Entity
            {
                Name = "SpriteThumbnailPreviewEntity",
            };

            _previewSpriteComponent = new StaticSpriteComponent();
            _previewEntity.RootComponent = _previewSpriteComponent;
            world.AddEntity(_previewEntity);
        });
    }

    private void EnsureRenderViewCreated()
    {
        if (_renderView != null)
        {
            return;
        }

        EnsurePreviewSceneCreated();

        _cameraEntity = new Entity
        {
            Name = "SpriteThumbnailCamera",
            IsVisible = false,
        };

        _camera = new CameraLookAtComponent();
        _cameraEntity.AddComponent(_camera);
        _cameraEntity.Initialize();
        _cameraEntity.InitializeWithWorld(_previewWorldDriver.World!);
        _camera.OnScreenResized(_thumbnailSize, _thumbnailSize);

        _surface = new RenderTargetSurface(
            _graphicsDevice,
            _thumbnailSize,
            _thumbnailSize,
            renderTargetPool: _editorRuntime.RenderTargetPool);

        ViewId viewId = _editorRuntime.GameManager.ViewManager.CreateView(new ViewDefinition
        {
            Name = "Sprite Thumbnail",
            World = _previewWorldDriver.World!,
            Camera = _camera,
            Surface = _surface,
            ClearColor = Microsoft.Xna.Framework.Color.Transparent,
            UpdateMode = ViewUpdateMode.OnDemand,
        });

        if (!_editorRuntime.GameManager.ViewManager.TryGetView(viewId, out RenderView? renderView))
        {
            throw new InvalidOperationException("The sprite thumbnail renderer could not create its offscreen render view.");
        }

        _renderView = renderView;
    }

    private void CacheSpriteAsset(SpriteData spriteAsset)
    {
        Guid spriteAssetId = spriteAsset.AssetId != Guid.Empty ? spriteAsset.AssetId : spriteAsset.Id;
        AssetInfo? assetInfo = AssetCatalog.Get(spriteAssetId);
        if (assetInfo != null)
        {
            spriteAsset.AssetId = assetInfo.Id;
            spriteAsset.Name = assetInfo.Name;
            spriteAsset.FileName = assetInfo.FileName;
            _editorRuntime.AssetContentManager.AddAsset(assetInfo, spriteAsset);
            return;
        }

        _editorRuntime.AssetContentManager.AddAsset(spriteAssetId, spriteAsset.Name, spriteAsset);
    }

    private void ConfigureCamera(SpriteData spriteAsset)
    {
        if (_camera == null)
        {
            return;
        }

        BoundingBox bounds = SpriteDataBoundsCalculator.CalculateLocalBounds(spriteAsset);
        float width = Math.Max(1f, bounds.Max.X - bounds.Min.X);
        float height = Math.Max(1f, bounds.Max.Y - bounds.Min.Y);
        float availableWidth = Math.Max(1f, _thumbnailSize - 16f);
        float availableHeight = Math.Max(1f, _thumbnailSize - 16f);
        float pixelsPerUnit = Math.Min(availableWidth / width, availableHeight / height);

        float verticalHalfFov = Math.Max(0.01f, _camera.FieldOfView * 0.5f);
        float halfDepth = Math.Max(0f, (bounds.Max.Z - bounds.Min.Z) * 0.5f);
        float distance = (_thumbnailSize * 0.5f) / (pixelsPerUnit * MathF.Tan(verticalHalfFov));
        distance = Math.Clamp(distance + halfDepth + 2f, 0.5f, 1000f);

        Vector3 focusTarget = (bounds.Min + bounds.Max) * 0.5f;
        _camera.SetPositionAndTarget(new Vector3(focusTarget.X, focusTarget.Y, distance), focusTarget);
    }

    private static byte[] CropTransparentPadding(byte[] imageBytes, out XnaPoint sourceSize)
    {
        using var input = new MemoryStream(imageBytes, writable: false);
        using var bitmap = new DrawingBitmap(input);
        DrawingRectangle cropBounds = FindVisibleBounds(bitmap);
        sourceSize = new XnaPoint(cropBounds.Width, cropBounds.Height);

        if (cropBounds.Width <= 0 || cropBounds.Height <= 0
            || (cropBounds.X == 0 && cropBounds.Y == 0 && cropBounds.Width == bitmap.Width && cropBounds.Height == bitmap.Height))
        {
            sourceSize = new XnaPoint(bitmap.Width, bitmap.Height);
            return imageBytes;
        }

        using var cropped = new DrawingBitmap(cropBounds.Width, cropBounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = DrawingGraphics.FromImage(cropped);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.DrawImage(bitmap, new DrawingRectangle(0, 0, cropBounds.Width, cropBounds.Height), cropBounds, GraphicsUnit.Pixel);

        using var output = new MemoryStream();
        cropped.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    private static DrawingRectangle FindVisibleBounds(DrawingBitmap bitmap)
    {
        int minX = bitmap.Width;
        int minY = bitmap.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A == 0)
                {
                    continue;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
        }

        return new DrawingRectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}