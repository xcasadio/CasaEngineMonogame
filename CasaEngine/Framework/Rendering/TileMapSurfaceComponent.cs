using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Renders a <see cref="TileMapComponent"/> into a <see cref="RenderTarget2D"/> through an internal
/// orthographic pass, so that the map is always painted in its natural pixel space and the resulting
/// texture can be mapped onto a 3D quad (arcade screen, minimap, in-game display).
///
/// Same ownership model as <see cref="UI.WorldUIComponent"/>: the surface is a plain owned object,
/// registered on the world with <see cref="Scene.World.World.RegisterTileMapSurface"/> and drawn once
/// per frame before the world pass.
///
/// Consumers should set <see cref="Materials.MaterialBase.SamplerState"/> to
/// <see cref="SamplerState.PointClamp"/> on the material displaying <see cref="Texture"/> to keep the
/// tiles crisp.
/// </summary>
public sealed class TileMapSurfaceComponent : IDisposable
{
    /// <summary>Largest render target dimension the surface will allocate.</summary>
    public const int MaximumSurfaceSize = 4096;

    // The orthographic camera sits in front of the map plane; near/far keep the layer z offsets of a
    // map authored in pixels well inside the depth range.
    private const float CameraDistance = 500.0f;
    private const float NearPlane = 1.0f;
    private const float FarPlane = 1000.0f;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly RenderTargetSurface _surface;
    private readonly List<Action<Texture2D>> _textureBindings = [];

    private TileMapComponent _tileMap;
    private int _zoom = 1;
    private bool _invalidated = true;
    private bool _neverRendered = true;
    private uint _lastRenderedTileRevision;
    private bool _disposed;

    /// <summary>The tile map painted into the surface.</summary>
    public TileMapComponent TileMap
    {
        get => _tileMap;
        set
        {
            if (ReferenceEquals(_tileMap, value))
            {
                return;
            }

            _tileMap = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Integer magnification of the render target relative to the map size in pixels
    /// (1 = one texel per map pixel). Values below 1 are clamped.
    /// </summary>
    public int Zoom
    {
        get => _zoom;
        set
        {
            var zoom = Math.Max(1, value);
            if (zoom == _zoom)
            {
                return;
            }

            _zoom = zoom;
            Invalidate();
        }
    }

    /// <summary>Clear color used before drawing the map.</summary>
    public Color ClearColor { get; set; } = Color.Transparent;

    /// <summary>The render target the tile map is painted into.</summary>
    public RenderTarget2D RenderTarget => _surface.RenderTarget;

    /// <summary>The render surface backing the offscreen pass.</summary>
    public RenderTargetSurface Surface => _surface;

    /// <summary>Alias of <see cref="RenderTarget"/> for material or quad consumers.</summary>
    public Texture2D Texture => _surface.Texture;

    /// <summary>Current size of the produced texture, in pixels.</summary>
    public Point Resolution { get; private set; }

    public TileMapSurfaceComponent(GraphicsDevice graphicsDevice, TileMapComponent tileMap = null, int zoom = 1)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        _graphicsDevice = graphicsDevice;
        _tileMap = tileMap;
        _zoom = Math.Max(1, zoom);

        TryGetSurfaceSize(_tileMap, _zoom, out var width, out var height);
        _surface = new RenderTargetSurface(
            graphicsDevice,
            width,
            height,
            SurfaceFormat.Color,
            DepthFormat.Depth24);
        Resolution = new Point(width, height);
    }

    /// <summary>Forces the surface to be repainted during the next offscreen pass.</summary>
    public void Invalidate()
    {
        _invalidated = true;
    }

    /// <summary>Binds the produced texture to an unlit material.</summary>
    public void BindToMaterial(UnlitTextureMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        BindTexture(texture => material.BasColor = texture);
    }

    /// <summary>Binds the produced texture to a lit material.</summary>
    public void BindToMaterial(LitDiffuseMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        BindTexture(texture => material.BasColor = texture);
    }

    public void BindTexture(Action<Texture2D> textureBinding)
    {
        ArgumentNullException.ThrowIfNull(textureBinding);

        _textureBindings.Add(textureBinding);
        textureBinding(Texture);
    }

    /// <summary>
    /// Repaints the surface when the map content changed since the last pass. A static map costs a
    /// single revision comparison per frame and no GPU work at all.
    /// </summary>
    public void DrawToTexture()
    {
        if (_disposed)
        {
            return;
        }

        var tileMap = _tileMap;
        if (tileMap?.TileMapData == null || tileMap.TileSetData == null)
        {
            return;
        }

        var world = tileMap.Owner?.World;
        if (world == null)
        {
            return;
        }

        if (!ShouldRedraw(
                _neverRendered,
                _invalidated,
                tileMap.HasAnimatedTiles,
                tileMap.HasDirtyAutoTiles,
                tileMap.TileRevision,
                _lastRenderedTileRevision))
        {
            return;
        }

        if (!TryGetSurfaceSize(tileMap, _zoom, out var width, out var height))
        {
            return;
        }

        EnsureSurfaceSize(width, height);

        var frame = BuildRenderFrame(tileMap, width, height);
        var spriteRenderer = world.Game?.GetGameComponent<SpriteRendererComponent>();

        using var guard = new GraphicsStateGuard(_graphicsDevice);

        // TileMapComponent.Draw reads the world render frame for culling and for the static chunk
        // batches: install the orthographic frame for the pass and put the previous one back after.
        var previousFrame = world.SetCurrentRenderFrame(frame);
        try
        {
            _surface.Apply(_graphicsDevice);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, ClearColor, 1.0f, 0);
            tileMap.DrawTileMap();

            // Flushing here both paints the dynamic tiles into the surface and empties the sprite
            // queues, so the main world pass never sees sprites queued with the orthographic frame.
            spriteRenderer?.Flush(in frame);
        }
        finally
        {
            world.SetCurrentRenderFrame(previousFrame);
        }

        _neverRendered = false;
        _invalidated = false;
        _lastRenderedTileRevision = tileMap.TileRevision;
    }

    /// <summary>
    /// Redraw predicate of the surface, kept free of any graphics dependency so that the dirty
    /// tracking can be unit tested.
    /// </summary>
    internal static bool ShouldRedraw(
        bool neverRendered,
        bool invalidated,
        bool hasAnimatedTiles,
        bool hasDirtyAutoTiles,
        uint tileRevision,
        uint lastRenderedTileRevision)
    {
        return neverRendered
            || invalidated
            || hasAnimatedTiles
            || hasDirtyAutoTiles
            || tileRevision != lastRenderedTileRevision;
    }

    /// <summary>
    /// Computes the render target size for a map: the map size in pixels multiplied by the zoom,
    /// scaled down uniformly when it would exceed <see cref="MaximumSurfaceSize"/>.
    /// </summary>
    internal static bool TryGetSurfaceSize(TileMapComponent tileMap, int zoom, out int width, out int height)
    {
        width = 1;
        height = 1;

        if (tileMap?.TileMapData == null || tileMap.TileSetData == null)
        {
            return false;
        }

        var mapPixelWidth = tileMap.TileMapData.MapSize.Width * tileMap.TileSetData.TileSize.Width;
        var mapPixelHeight = tileMap.TileMapData.MapSize.Height * tileMap.TileSetData.TileSize.Height;

        if (mapPixelWidth <= 0 || mapPixelHeight <= 0)
        {
            return false;
        }

        var zoomedWidth = (long)mapPixelWidth * Math.Max(1, zoom);
        var zoomedHeight = (long)mapPixelHeight * Math.Max(1, zoom);

        if (zoomedWidth > MaximumSurfaceSize || zoomedHeight > MaximumSurfaceSize)
        {
            var reduction = Math.Min(
                MaximumSurfaceSize / (double)zoomedWidth,
                MaximumSurfaceSize / (double)zoomedHeight);
            zoomedWidth = (long)(zoomedWidth * reduction);
            zoomedHeight = (long)(zoomedHeight * reduction);
        }

        width = (int)Math.Clamp(zoomedWidth, 1, MaximumSurfaceSize);
        height = (int)Math.Clamp(zoomedHeight, 1, MaximumSurfaceSize);
        return true;
    }

    private void EnsureSurfaceSize(int width, int height)
    {
        if (Resolution.X == width && Resolution.Y == height && _surface.RenderTarget != null)
        {
            return;
        }

        _surface.EnsureSize(width, height);
        Resolution = new Point(width, height);
        RefreshBindings();
    }

    /// <summary>
    /// Builds the orthographic frame framing the whole map: same axis convention as
    /// <see cref="Camera2dComponent"/> (world units are pixels, +Y up, camera looking along -Z).
    /// The map occupies [0, width] x [-height, 0] in the component local space.
    /// </summary>
    private static RenderFrame BuildRenderFrame(TileMapComponent tileMap, int width, int height)
    {
        var mapPixelWidth = tileMap.TileMapData.MapSize.Width * tileMap.TileSetData.TileSize.Width;
        var mapPixelHeight = tileMap.TileMapData.MapSize.Height * tileMap.TileSetData.TileSize.Height;

        var scale = tileMap.Scale;
        var position = tileMap.Position;
        var worldWidth = mapPixelWidth * scale.X;
        var worldHeight = mapPixelHeight * scale.Y;

        var target = new Vector3(
            position.X + worldWidth * 0.5f,
            position.Y - worldHeight * 0.5f,
            position.Z);
        var cameraPosition = new Vector3(target.X, target.Y, target.Z + CameraDistance);

        var view = Matrix.CreateLookAt(cameraPosition, target, Vector3.Up);
        var projection = Matrix.CreateOrthographic(worldWidth, worldHeight, NearPlane, FarPlane);

        return new RenderFrame(view, projection, cameraPosition, new Rectangle(0, 0, width, height));
    }

    private void RefreshBindings()
    {
        var texture = Texture;
        for (var index = 0; index < _textureBindings.Count; index++)
        {
            _textureBindings[index](texture);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _surface.Dispose();
    }
}
