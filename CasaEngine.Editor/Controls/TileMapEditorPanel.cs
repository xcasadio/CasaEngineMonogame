using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Geometry;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;
using AssetTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Editor.Controls;

internal sealed class TileMapEditorPanel : IDisposable
{
    private const float MinZoom = 0.125f;
    private const float MaxZoom = 8.0f;

    private sealed class AnimatedCellState
    {
        public AnimatedTileData TileData { get; init; } = null!;
        public TileSetData TileSetData { get; init; } = null!;
        public Rectangle CurrentSourceRectangle { get; private set; }
        private int _currentFrameIndex;
        private float _elapsedMilliseconds;

        public void Reset()
        {
            _currentFrameIndex = 0;
            _elapsedMilliseconds = 0f;
            CurrentSourceRectangle = ResolveCurrentSourceRectangle();
        }

        public bool Update(float elapsedSeconds)
        {
            if (TileData.Frames.Count <= 1)
            {
                return false;
            }

            _elapsedMilliseconds += elapsedSeconds * 1000f;
            var frameChanged = false;
            while (_elapsedMilliseconds >= GetCurrentDuration())
            {
                _elapsedMilliseconds -= GetCurrentDuration();
                _currentFrameIndex++;
                if (_currentFrameIndex >= TileData.Frames.Count)
                {
                    _currentFrameIndex = 0;
                }

                frameChanged = true;
            }

            if (frameChanged)
            {
                CurrentSourceRectangle = ResolveCurrentSourceRectangle();
            }

            return frameChanged;
        }

        private int GetCurrentDuration()
        {
            var duration = TileData.Frames[_currentFrameIndex].DurationMilliseconds;
            return duration <= 0 ? 1 : duration;
        }

        private Rectangle ResolveCurrentSourceRectangle()
        {
            if (TileData.Frames.Count == 0)
            {
                return TileData.Location;
            }

            var frame = TileData.Frames[_currentFrameIndex];
            return TileSetData.TryGetTileSourceRectangle(frame.TileId, out var sourceRectangle)
                ? sourceRectangle
                : TileData.Location;
        }
    }

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly List<TileSetData> _tileSets = new();
    private readonly List<Texture2D> _tileSetTextures = new();
    private readonly List<bool> _layerVisibility = new();

    private MGDockPanel? _root;
    private MGDockPanel? _viewportHost;
    private MGImage? _viewportImage;
    private MGTextBlock? _statusText;
    private MGCheckBox? _showCollisionCheckBox;
    private SpriteBatch? _spriteBatch;
    private RenderTarget2D? _renderTarget;
    private Texture2D? _whitePixel;
    private Texture2D? _boundTexture;
    private AnimatedCellState?[] _animatedCells = Array.Empty<AnimatedCellState?>();
    private TileMapData? _tileMapData;
    private string? _loadedRelativePath;
    private string _statusMessage = "Open a .tileMap asset from the Content Browser.";
    private Vector2 _cameraOffset = new(24f, 24f);
    private Vector2 _dragStartCameraOffset;
    private float _zoom = 1.0f;
    private int _rtWidth = 320;
    private int _rtHeight = 240;
    private int _selectedLayerIndex = -1;
    private bool _showCollisions;
    private bool _isPanning;
    private bool _needsRender = true;
    private bool _disposed;

    public TileMapEditorPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
    }

    public TileMapData? LoadedTileMap => _tileMapData;
    public string? LoadedRelativePath => _loadedRelativePath;
    public int SelectedLayerIndex => _selectedLayerIndex;

    public TileMapLayerData? SelectedLayer => _tileMapData != null
        && _selectedLayerIndex >= 0
        && _selectedLayerIndex < _tileMapData.Layers.Count
            ? _tileMapData.Layers[_selectedLayerIndex]
            : null;

    public bool ShowCollisions
    {
        get => _showCollisions;
        set
        {
            if (_showCollisions == value)
            {
                return;
            }

            _showCollisions = value;
            if (_showCollisionCheckBox != null && _showCollisionCheckBox.IsChecked != value)
            {
                _showCollisionCheckBox.IsChecked = value;
            }

            _needsRender = true;
        }
    }

    public event Action? LayersChanged;
    public event Action<int>? SelectedLayerChanged;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        EnsureRenderTarget();
        EnsureWhitePixel();

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 8,
            Margin = new Thickness(4, 4, 4, 2),
        };

        toolbar.TryAddChild(CreateButton("100%", ResetZoom100));
        _showCollisionCheckBox = CreateCheckBox("Collisions", ShowCollisions, isChecked => ShowCollisions = isChecked == true);
        toolbar.TryAddChild(_showCollisionCheckBox);

        _statusText = new MGTextBlock(_window, EscapeMarkup(_statusMessage))
        {
            Margin = new Thickness(4, 0, 4, 4),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _viewportHost = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 260,
            IsFocusable = true,
        };
        _viewportHost.OnLayoutBoundsChanged += OnViewportBoundsChanged;
        _viewportHost.MouseHandler.DragStart += OnViewportDragStart;
        _viewportHost.MouseHandler.Dragged += OnViewportDragged;
        _viewportHost.MouseHandler.DragEnd += OnViewportDragEnd;
        _viewportHost.MouseHandler.Scrolled += OnViewportScrolled;

        _viewportImage = new MGImage(_window, new MGTextureData(EditorIcons.AsImage(_renderTarget!)!), Stretch: Stretch.Fill)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
        _viewportHost.TryAddChild(_viewportImage, Dock.Top);

        var viewportBorder = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground)),
            Margin = new Thickness(4, 0, 4, 4),
            Padding = new Thickness(1),
            MinHeight = 260,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        viewportBorder.SetContent(_viewportHost);

        _root = new MGDockPanel(_window)
        {
            Margin = new Thickness(0, 4, 0, 0),
        };
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(viewportBorder, Dock.Top);

        _needsRender = true;
        return _root;
    }

    public void LoadAsset(TileMapData tileMapData, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(tileMapData);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _tileMapData = tileMapData;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        LoadTileSets();
        RebuildLayerVisibility();
        RebuildAnimatedCells();
        SelectLayer(_tileMapData.Layers.Count > 0 ? 0 : -1);
        _cameraOffset = new Vector2(24f, 24f);
        _zoom = 1.0f;
        _statusMessage = $"{_tileMapData.Name}: {_tileMapData.MapSize.Width}x{_tileMapData.MapSize.Height}, {_tileMapData.Layers.Count} layer(s)";
        RefreshStatusText();
        _needsRender = true;
        LayersChanged?.Invoke();
    }

    public void Update(GameTime gameTime)
    {
        if (_animatedCells.Length == 0)
        {
            return;
        }

        var changed = false;
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (var index = 0; index < _animatedCells.Length; index++)
        {
            var animatedCell = _animatedCells[index];
            if (animatedCell != null && animatedCell.Update(elapsedSeconds))
            {
                changed = true;
            }
        }

        if (changed)
        {
            _needsRender = true;
        }
    }

    public void DrawViewport(GameTime gameTime)
    {
        if (_root == null || _disposed || !EnsureRenderTarget())
        {
            return;
        }

        if (!_needsRender && !HasAnimatedCells())
        {
            RefreshTextureBinding();
            return;
        }

        RenderTileMap();
        RefreshTextureBinding();
    }

    public void SelectLayer(int layerIndex)
    {
        var normalizedLayerIndex = NormalizeLayerIndex(layerIndex);
        if (_selectedLayerIndex == normalizedLayerIndex)
        {
            return;
        }

        _selectedLayerIndex = normalizedLayerIndex;
        SelectedLayerChanged?.Invoke(_selectedLayerIndex);
    }

    public string GetLayerDisplayName(int layerIndex)
    {
        if (_tileMapData == null || layerIndex < 0 || layerIndex >= _tileMapData.Layers.Count)
        {
            return string.Empty;
        }

        var layer = _tileMapData.Layers[layerIndex];
        return string.IsNullOrWhiteSpace(layer.Name) ? $"Layer {layerIndex}" : layer.Name!;
    }

    public bool IsLayerVisible(int layerIndex)
    {
        return layerIndex >= 0 && layerIndex < _layerVisibility.Count && _layerVisibility[layerIndex];
    }

    public void SetLayerVisible(int layerIndex, bool isVisible)
    {
        if (layerIndex < 0 || layerIndex >= _layerVisibility.Count || _layerVisibility[layerIndex] == isVisible)
        {
            return;
        }

        _layerVisibility[layerIndex] = isVisible;
        _needsRender = true;
        LayersChanged?.Invoke();
    }

    public int CountNonEmptyTiles(int layerIndex)
    {
        if (_tileMapData == null || layerIndex < 0 || layerIndex >= _tileMapData.Layers.Count)
        {
            return 0;
        }

        var layer = _tileMapData.Layers[layerIndex];
        var count = 0;
        for (var index = 0; index < layer.tiles.Count; index++)
        {
            if (layer.tiles[index] != TileMapData.EmptyTileId)
            {
                count++;
            }
        }

        return count;
    }

    public int CountCollisionTiles(int layerIndex)
    {
        if (_tileMapData == null || layerIndex < 0 || layerIndex >= _tileMapData.Layers.Count)
        {
            return 0;
        }

        var layer = _tileMapData.Layers[layerIndex];
        var count = 0;
        for (var tileIndex = 0; tileIndex < layer.tiles.Count; tileIndex++)
        {
            if (TryGetTileData(layer, tileIndex, out var tileData) && tileData?.CollisionType != TileCollisionType.None)
            {
                count++;
            }
        }

        return count;
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        var result = new List<string>(8)
        {
            $"Asset: {_tileMapData?.Name ?? "<none>"}",
            $"Path: {_loadedRelativePath ?? "<none>"}",
            $"Map size: {DescribeMapSize()}",
            $"Layers: {_tileMapData?.Layers.Count ?? 0}",
            $"Selected layer: {_selectedLayerIndex}",
            $"Zoom: {_zoom:0.###}",
            $"Show collisions: {_showCollisions}",
        };

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_viewportHost != null)
        {
            _viewportHost.OnLayoutBoundsChanged -= OnViewportBoundsChanged;
            _viewportHost.MouseHandler.DragStart -= OnViewportDragStart;
            _viewportHost.MouseHandler.Dragged -= OnViewportDragged;
            _viewportHost.MouseHandler.DragEnd -= OnViewportDragEnd;
            _viewportHost.MouseHandler.Scrolled -= OnViewportScrolled;
        }

        _renderTarget?.Dispose();
        _whitePixel?.Dispose();
        _spriteBatch?.Dispose();
    }

    internal static bool TryLoadAsset(string fullPath, out TileMapData tileMapData)
    {
        tileMapData = new TileMapData();

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["map_size"] == null || document["layers"] == null)
            {
                return false;
            }

            tileMapData.Load(document);
            tileMapData.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
            var normalizedFileName = tileMapData.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var assetInfo = AssetCatalog.GetByFileName(tileMapData.FileName) ?? AssetCatalog.GetByFileName(normalizedFileName);
            if (assetInfo != null)
            {
                tileMapData.Name = assetInfo.Name;
                tileMapData.AssetId = assetInfo.Id;
                tileMapData.FileName = assetInfo.FileName;
            }
            else
            {
                tileMapData.AssetId = tileMapData.Id;
            }

            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteWarning($"Failed to load TileMap asset '{fullPath}': {exception.Message}");
            return false;
        }
    }

    private void LoadTileSets()
    {
        _tileSets.Clear();
        _tileSetTextures.Clear();

        if (_tileMapData == null)
        {
            return;
        }

        for (var tileSetIndex = 0; tileSetIndex < _tileMapData.TileSetDataAssetIds.Count; tileSetIndex++)
        {
            var tileSetData = _editorRuntime.AssetContentManager.Load<TileSetData>(_tileMapData.TileSetDataAssetIds[tileSetIndex]);
            var texture = _editorRuntime.AssetContentManager.Load<AssetTexture>(tileSetData.SpriteSheetAssetId);
            texture.Load(_editorRuntime.AssetContentManager);
            if (texture.Resource == null)
            {
                throw new InvalidOperationException($"TileSet '{tileSetData.Name}' has no loaded texture resource.");
            }

            _tileSets.Add(tileSetData);
            _tileSetTextures.Add(texture.Resource);
        }
    }

    private void RebuildLayerVisibility()
    {
        _layerVisibility.Clear();
        var layerCount = _tileMapData?.Layers.Count ?? 0;
        for (var layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            _layerVisibility.Add(true);
        }
    }

    private void RebuildAnimatedCells()
    {
        _animatedCells = Array.Empty<AnimatedCellState?>();
        if (_tileMapData == null || _tileMapData.Layers.Count == 0)
        {
            return;
        }

        var tileCount = _tileMapData.MapSize.Width * _tileMapData.MapSize.Height;
        _animatedCells = new AnimatedCellState[_tileMapData.Layers.Count * tileCount];
        for (var layerIndex = 0; layerIndex < _tileMapData.Layers.Count; layerIndex++)
        {
            var layer = _tileMapData.Layers[layerIndex];
            for (var tileIndex = 0; tileIndex < layer.tiles.Count; tileIndex++)
            {
                if (!TryGetTileData(layer, tileIndex, out var tileData) || tileData is not AnimatedTileData animatedTileData)
                {
                    continue;
                }

                var tileSourceIndex = layer.GetTileSourceIndex(tileIndex);
                if (tileSourceIndex < 0 || tileSourceIndex >= _tileSets.Count)
                {
                    continue;
                }

                var animatedCell = new AnimatedCellState
                {
                    TileData = animatedTileData,
                    TileSetData = _tileSets[tileSourceIndex],
                };
                animatedCell.Reset();
                _animatedCells[layerIndex * tileCount + tileIndex] = animatedCell;
            }
        }
    }

    private bool HasAnimatedCells()
    {
        for (var index = 0; index < _animatedCells.Length; index++)
        {
            if (_animatedCells[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RenderTileMap()
    {
        if (_renderTarget == null)
        {
            return;
        }

        var previousTargets = _graphicsDevice.GetRenderTargets();
        var previousViewport = _graphicsDevice.Viewport;
        var previousBlendState = _graphicsDevice.BlendState;
        var previousDepthStencilState = _graphicsDevice.DepthStencilState;
        var previousRasterizerState = _graphicsDevice.RasterizerState;
        var previousSamplerState = _graphicsDevice.SamplerStates[0];

        try
        {
            _graphicsDevice.SetRenderTarget(_renderTarget);
            _graphicsDevice.Clear(EditorThemePalette.PreviewClearColor);

            if (_tileMapData != null && _tileSets.Count > 0)
            {
                _spriteBatch ??= new SpriteBatch(_graphicsDevice);
                var transform = Matrix.CreateScale(_zoom, _zoom, 1.0f)
                    * Matrix.CreateTranslation(_cameraOffset.X, _cameraOffset.Y, 0.0f);
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, transform);
                DrawTileLayers(_spriteBatch);
                if (_showCollisions)
                {
                    DrawCollisionOverlay(_spriteBatch);
                }

                _spriteBatch.End();
            }
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(previousTargets);
            _graphicsDevice.Viewport = previousViewport;
            _graphicsDevice.BlendState = previousBlendState;
            _graphicsDevice.DepthStencilState = previousDepthStencilState;
            _graphicsDevice.RasterizerState = previousRasterizerState;
            _graphicsDevice.SamplerStates[0] = previousSamplerState;
        }

        _needsRender = false;
    }

    private void DrawTileLayers(SpriteBatch spriteBatch)
    {
        if (_tileMapData == null || _tileSets.Count == 0)
        {
            return;
        }

        var tileWidth = _tileSets[0].TileSize.Width;
        var tileHeight = _tileSets[0].TileSize.Height;
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return;
        }

        GetVisibleTileRange(tileWidth, tileHeight, out var minTileX, out var maxTileX, out var minTileY, out var maxTileY);
        var tileCount = _tileMapData.MapSize.Width * _tileMapData.MapSize.Height;
        for (var layerIndex = 0; layerIndex < _tileMapData.Layers.Count; layerIndex++)
        {
            if (!IsLayerVisible(layerIndex))
            {
                continue;
            }

            var layer = _tileMapData.Layers[layerIndex];
            for (var y = minTileY; y <= maxTileY; y++)
            {
                var rowOffset = y * _tileMapData.MapSize.Width;
                for (var x = minTileX; x <= maxTileX; x++)
                {
                    var tileIndex = rowOffset + x;
                    if (tileIndex < 0 || tileIndex >= layer.tiles.Count || layer.tiles[tileIndex] == TileMapData.EmptyTileId)
                    {
                        continue;
                    }

                    if (!TryGetTileRenderData(layer, layerIndex, tileIndex, tileCount, out var texture, out var sourceRectangle))
                    {
                        continue;
                    }

                    var destination = new Rectangle(x * tileWidth, y * tileHeight, tileWidth, tileHeight);
                    spriteBatch.Draw(texture, destination, sourceRectangle, Color.White, 0f, Vector2.Zero, GetSpriteEffects(layer.GetTileFlags(tileIndex)), 0f);
                }
            }
        }
    }

    private void DrawCollisionOverlay(SpriteBatch spriteBatch)
    {
        if (_tileMapData == null || _tileSets.Count == 0 || _whitePixel == null)
        {
            return;
        }

        var tileWidth = _tileSets[0].TileSize.Width;
        var tileHeight = _tileSets[0].TileSize.Height;
        GetVisibleTileRange(tileWidth, tileHeight, out var minTileX, out var maxTileX, out var minTileY, out var maxTileY);

        for (var layerIndex = 0; layerIndex < _tileMapData.Layers.Count; layerIndex++)
        {
            if (!IsLayerVisible(layerIndex))
            {
                continue;
            }

            var layer = _tileMapData.Layers[layerIndex];
            for (var y = minTileY; y <= maxTileY; y++)
            {
                var rowOffset = y * _tileMapData.MapSize.Width;
                for (var x = minTileX; x <= maxTileX; x++)
                {
                    var tileIndex = rowOffset + x;
                    if (!TryGetTileData(layer, tileIndex, out var tileData) || tileData == null || tileData.CollisionType == TileCollisionType.None)
                    {
                        continue;
                    }

                    var collisionRectangle = GetCollisionRectangle(tileData, x, y, tileWidth, tileHeight);
                    var color = tileData.CollisionType == TileCollisionType.NoContactResponse
                        ? new Color(255, 210, 86, 210)
                        : new Color(255, 72, 72, 220);
                    DrawRectangleFill(spriteBatch, collisionRectangle, color * 0.18f);
                    DrawRectangleOutline(spriteBatch, collisionRectangle, color, 2);
                }
            }
        }
    }

    private Rectangle GetCollisionRectangle(TileData tileData, int tileX, int tileY, int tileWidth, int tileHeight)
    {
        if (tileData.CollisionShape?.Shape is ShapeRectangle rectangle && rectangle.Width > 0f && rectangle.Height > 0f)
        {
            return new Rectangle(
                tileX * tileWidth + (int)MathF.Round(rectangle.Position.X),
                tileY * tileHeight + (int)MathF.Round(rectangle.Position.Y),
                (int)MathF.Round(rectangle.Width),
                (int)MathF.Round(rectangle.Height));
        }

        return new Rectangle(tileX * tileWidth, tileY * tileHeight, tileWidth, tileHeight);
    }

    private void DrawRectangleFill(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
    {
        if (_whitePixel != null)
        {
            spriteBatch.Draw(_whitePixel, rectangle, color);
        }
    }

    private void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
    {
        if (_whitePixel == null || rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        spriteBatch.Draw(_whitePixel, new Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, thickness), color);
        spriteBatch.Draw(_whitePixel, new Rectangle(rectangle.Left, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(_whitePixel, new Rectangle(rectangle.Left, rectangle.Top, thickness, rectangle.Height), color);
        spriteBatch.Draw(_whitePixel, new Rectangle(rectangle.Right - thickness, rectangle.Top, thickness, rectangle.Height), color);
    }

    private bool TryGetTileRenderData(
        TileMapLayerData layer,
        int layerIndex,
        int tileIndex,
        int tileCount,
        out Texture2D texture,
        out Rectangle sourceRectangle)
    {
        texture = null!;
        sourceRectangle = Rectangle.Empty;

        var tileSourceIndex = layer.GetTileSourceIndex(tileIndex);
        if (tileSourceIndex < 0 || tileSourceIndex >= _tileSets.Count || tileSourceIndex >= _tileSetTextures.Count)
        {
            return false;
        }

        if (!TryGetTileData(layer, tileIndex, out var tileData) || tileData == null)
        {
            return false;
        }

        if (tileData is AnimatedTileData)
        {
            var animatedCell = _animatedCells.Length > 0 ? _animatedCells[layerIndex * tileCount + tileIndex] : null;
            if (animatedCell != null)
            {
                sourceRectangle = animatedCell.CurrentSourceRectangle;
            }
            else if (!_tileSets[tileSourceIndex].TryGetTileSourceRectangle(tileData.Id, out sourceRectangle))
            {
                return false;
            }
        }
        else if (tileData is AutoTileData autoTileData)
        {
            sourceRectangle = autoTileData.Locations[0];
        }
        else if (!_tileSets[tileSourceIndex].TryGetTileSourceRectangle(tileData.Id, out sourceRectangle))
        {
            return false;
        }

        texture = _tileSetTextures[tileSourceIndex];
        return sourceRectangle.Width > 0 && sourceRectangle.Height > 0;
    }

    private bool TryGetTileData(TileMapLayerData layer, int tileIndex, out TileData? tileData)
    {
        tileData = null;
        if (tileIndex < 0 || tileIndex >= layer.tiles.Count)
        {
            return false;
        }

        var tileId = layer.tiles[tileIndex];
        if (tileId == TileMapData.EmptyTileId)
        {
            return false;
        }

        var tileSourceIndex = layer.GetTileSourceIndex(tileIndex);
        return tileSourceIndex >= 0
            && tileSourceIndex < _tileSets.Count
            && _tileSets[tileSourceIndex].TryGetTileData(tileId, out tileData);
    }

    private void GetVisibleTileRange(int tileWidth, int tileHeight, out int minTileX, out int maxTileX, out int minTileY, out int maxTileY)
    {
        minTileX = 0;
        maxTileX = Math.Max(0, (_tileMapData?.MapSize.Width ?? 1) - 1);
        minTileY = 0;
        maxTileY = Math.Max(0, (_tileMapData?.MapSize.Height ?? 1) - 1);

        if (_tileMapData == null || tileWidth <= 0 || tileHeight <= 0 || _zoom <= 0f)
        {
            return;
        }

        var worldLeft = -_cameraOffset.X / _zoom;
        var worldTop = -_cameraOffset.Y / _zoom;
        var worldRight = (_rtWidth - _cameraOffset.X) / _zoom;
        var worldBottom = (_rtHeight - _cameraOffset.Y) / _zoom;

        minTileX = Math.Clamp((int)MathF.Floor(worldLeft / tileWidth) - 1, 0, _tileMapData.MapSize.Width - 1);
        maxTileX = Math.Clamp((int)MathF.Ceiling(worldRight / tileWidth) + 1, 0, _tileMapData.MapSize.Width - 1);
        minTileY = Math.Clamp((int)MathF.Floor(worldTop / tileHeight) - 1, 0, _tileMapData.MapSize.Height - 1);
        maxTileY = Math.Clamp((int)MathF.Ceiling(worldBottom / tileHeight) + 1, 0, _tileMapData.MapSize.Height - 1);
    }

    private bool EnsureRenderTarget()
    {
        var width = Math.Max(64, _rtWidth);
        var height = Math.Max(64, _rtHeight);
        if (_renderTarget != null && _renderTarget.Width == width && _renderTarget.Height == height)
        {
            return true;
        }

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(_graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _boundTexture = null;
        _needsRender = true;
        return true;
    }

    private void EnsureWhitePixel()
    {
        if (_whitePixel != null)
        {
            return;
        }

        _whitePixel = new Texture2D(_graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whitePixel.SetData(new[] { Color.White });
    }

    private void RefreshTextureBinding()
    {
        if (_renderTarget == null || ReferenceEquals(_renderTarget, _boundTexture))
        {
            return;
        }

        _boundTexture = _renderTarget;
        if (_viewportImage != null)
        {
            _viewportImage.Source = new MGTextureData(EditorIcons.AsImage(_renderTarget)!);
        }
    }

    private void OnViewportBoundsChanged(object? sender, EventArgs<Rectangle> e)
    {
        var width = Math.Max(64, e.NewValue.Width);
        var height = Math.Max(64, e.NewValue.Height);
        if (width == _rtWidth && height == _rtHeight)
        {
            return;
        }

        _rtWidth = width;
        _rtHeight = height;
        _needsRender = true;
    }

    private void OnViewportDragStart(object? sender, BaseMouseDragStartEventArgs e)
    {
        if (!e.IsLMB && !e.IsMMB)
        {
            return;
        }

        _isPanning = true;
        _dragStartCameraOffset = _cameraOffset;
        e.SetHandledBy(_viewportHost ?? sender as IMouseHandlerHost);
    }

    private void OnViewportDragged(object? sender, BaseMouseDraggedEventArgs e)
    {
        if (!_isPanning || (!e.IsLMB && !e.IsMMB))
        {
            return;
        }

        var delta = e.PositionDelta;
        _cameraOffset = _dragStartCameraOffset + new Vector2(delta.X, delta.Y);
        _needsRender = true;
    }

    private void OnViewportDragEnd(object? sender, BaseMouseDragEndEventArgs e)
    {
        if (!e.IsLMB && !e.IsMMB)
        {
            return;
        }

        _isPanning = false;
    }

    private void OnViewportScrolled(object? sender, BaseMouseScrolledEventArgs e)
    {
        if (e.ScrollWheelDelta == 0 || _viewportHost == null || _viewportHost.Parent == null)
        {
            return;
        }

        var bounds = !_viewportHost.ActualLayoutBounds.IsEmpty ? _viewportHost.ActualLayoutBounds : _viewportHost.LayoutBounds;
        if (!bounds.Contains(e.Position))
        {
            return;
        }

        var wheelSteps = e.ScrollWheelDelta / 120.0f;
        var newZoom = MathHelper.Clamp(_zoom * MathF.Pow(1.1f, wheelSteps), MinZoom, MaxZoom);
        SetZoomAroundScreenPoint(newZoom, e.Position - new Point(bounds.X, bounds.Y));
        e.SetHandledBy(_viewportHost ?? sender as IMouseHandlerHost);
    }

    private void ResetZoom100()
    {
        SetZoomAroundScreenPoint(1.0f, new Point(_rtWidth / 2, _rtHeight / 2));
    }

    private void SetZoomAroundScreenPoint(float newZoom, Point screenPoint)
    {
        if (Math.Abs(newZoom - _zoom) < 0.0001f)
        {
            return;
        }

        var screen = new Vector2(screenPoint.X, screenPoint.Y);
        var worldBefore = (screen - _cameraOffset) / _zoom;
        _zoom = newZoom;
        _cameraOffset = screen - worldBefore * _zoom;
        _needsRender = true;
    }

    private int NormalizeLayerIndex(int layerIndex)
    {
        if (_tileMapData == null || _tileMapData.Layers.Count == 0)
        {
            return -1;
        }

        return Math.Clamp(layerIndex, 0, _tileMapData.Layers.Count - 1);
    }

    private string DescribeMapSize()
    {
        return _tileMapData == null
            ? "<none>"
            : $"{_tileMapData.MapSize.Width}x{_tileMapData.MapSize.Height}";
    }

    private void RefreshStatusText()
    {
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(_statusMessage);
        }
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 72,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private MGCheckBox CreateCheckBox(string label, bool isChecked, Action<bool?> onChanged)
    {
        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = isChecked,
        };
        checkBox.SetContent(new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        checkBox.OnCheckStateChanged += (_, args) => onChanged(args.NewValue);
        return checkBox;
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

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}