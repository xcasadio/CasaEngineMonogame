using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.EffectQuads;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Drives <see cref="EffectQuadService"/> from the game loop: resolves the service's sheet texture ids into <see cref="Texture2D"/>s
/// and submits every quad of the pushed frame through <c>SpriteRendererComponent.DrawPsxQuad</c>
/// (docs/engine/effect-quads.md, ADR-0071). Same house pattern as <see cref="CellularLayerComponent"/>.
///
/// Submission happens from this component's own <see cref="Update"/>, not <c>Draw</c>: the game DLL pushes this frame's quads to
/// <see cref="Service"/> from <c>GameManager.UpdateWorld</c>, which every <c>GameComponent.Update</c> (this one included) runs
/// after, so the frame read here is already final. The frame persists, so every drawn frame submits it once (a fixed time step that
/// ran several updates per drawn frame would queue it several times; the game of this repository does not use one).
/// The component does not sort: the order is the one of the sprite pass, from each quad's sort key.
/// </summary>
public class EffectQuadComponent : GameComponent
{
    private readonly CasaEngineGame _game;
    private Texture2D[] _sheets = System.Array.Empty<Texture2D>();
    private readonly System.Collections.Generic.List<AssetHandle<CasaEngineTexture>> _textureHandles = new();
    private readonly System.Func<System.Guid, Texture2D> _loadTexture;
    private int _resolvedSheetsVersion = -1;
    private bool _isDisposed;

    public EffectQuadComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game as CasaEngineGame;
        _loadTexture = LoadTexture;
        Service = new EffectQuadService();

        UpdateOrder = (int)ComponentUpdateOrder.EffectQuads;
        game.Components.Add(this);
    }

    /// <summary>The sheets, the pushed frame and the push contract.</summary>
    public EffectQuadService Service { get; }

    public override void Update(GameTime gameTime)
    {
        if (_resolvedSheetsVersion != Service.SheetsVersion)
        {
            ResolveTextures(_loadTexture);
        }

        if (Service.QuadCount > 0 && _game?.SpriteRendererComponent != null)
        {
            Submit(_game.SpriteRendererComponent, ResolveScissorRectangle());
        }

        base.Update(gameTime);
    }

    /// <summary>
    /// Resolves every sheet of <see cref="Service"/> through <paramref name="loader"/> (production: the asset manager, see
    /// <see cref="Update"/>) and records the <see cref="EffectQuadService.SheetsVersion"/> it resolved, so that the next
    /// <see cref="Update"/> does not resolve the same version again through the catalog - a demo or a test that supplies its own
    /// textures keeps them. An empty id (an absent sheet) resolves to no texture without calling the loader; a sheet that fails to
    /// load (a null texture or an exception of the loader) is logged once and skipped: any quad using it is not drawn.
    /// </summary>
    public void ResolveTextures(System.Func<System.Guid, Texture2D> loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        // Only runs on a sheets-version change (never per frame), so giving the previous holds back and taking new ones is no
        // hot-path cost (ADR-0037).
        ReleaseTextureHandles();

        var sheetCount = Service.SheetCount;
        var sheets = new Texture2D[sheetCount];
        for (var i = 0; i < sheetCount; i++)
        {
            var id = Service.GetSheetId(i);
            if (id == System.Guid.Empty)
            {
                continue;
            }

            Texture2D texture = null;
            var reason = "the loader returned no texture";
            try
            {
                texture = loader(id);
            }
            catch (System.Exception exception)
            {
                reason = exception.Message;
            }

            if (texture == null)
            {
                Logs.WriteWarning(
                    $"EffectQuadComponent: sheet {i} texture '{id}' failed to load ({reason}); quads using it are skipped.");
                continue;
            }

            sheets[i] = texture;
        }

        _sheets = sheets;
        _resolvedSheetsVersion = Service.SheetsVersion;
    }

    private Texture2D LoadTexture(System.Guid id)
    {
        if (_game == null)
        {
            return null;
        }

        var handle = _game.AssetContentManager.Acquire<CasaEngineTexture>(id);
        _textureHandles.Add(handle);
        if (handle.Asset.Resource == null)
        {
            handle.Asset.Load(_game.AssetContentManager);
        }

        return handle.Asset.Resource;
    }

    private void ReleaseTextureHandles()
    {
        for (var i = 0; i < _textureHandles.Count; i++)
        {
            _textureHandles[i].Dispose();
        }

        _textureHandles.Clear();
    }

    /// <summary>
    /// Submits every quad of the pushed frame, in the order of the frame, through the scissor-explicit
    /// <c>SpriteRendererComponent.DrawPsxQuad</c> overload - never the device's current scissor rectangle, so this method never touches
    /// <see cref="GraphicsDevice"/> and is exercisable headless. The raw window, the four world corners, the mode and the sort key go
    /// through unchanged, with <see cref="Color.White"/> and z = 0 (the polygon of the original is drawn raw). A quad whose sheet index
    /// is out of range, whose sheet is absent or whose sheet failed to resolve is skipped.
    /// </summary>
    public void Submit(SpriteRendererComponent renderer, Rectangle scissorRectangle)
    {
        if (renderer == null)
        {
            return;
        }

        var quadCount = Service.QuadCount;
        for (var i = 0; i < quadCount; i++)
        {
            var quad = Service.GetQuad(i);
            if ((uint)quad.SheetIndex >= (uint)_sheets.Length)
            {
                continue;
            }

            var texture = _sheets[quad.SheetIndex];
            if (texture == null)
            {
                continue;
            }

            var sortKey = quad.SortKey;
            renderer.DrawPsxQuad(
                texture,
                quad.Window,
                quad.TopLeft,
                quad.TopRight,
                quad.BottomLeft,
                quad.BottomRight,
                Color.White,
                0f,
                in sortKey,
                quad.Mode,
                scissorRectangle);
        }
    }

    private Rectangle ResolveScissorRectangle()
    {
        GraphicsDevice graphicsDevice;
        try
        {
            // Same guard as CellularLayerComponent: a headless/partially constructed Game can throw from the GraphicsDevice getter
            // itself rather than returning null - either way, no device means fall back to the pixel-size rectangle below.
            graphicsDevice = _game?.GraphicsDevice;
        }
        catch (System.Exception)
        {
            graphicsDevice = null;
        }

        if (graphicsDevice != null)
        {
            return graphicsDevice.ScissorRectangle;
        }

        var width = _game?.ScreenSizeWidth ?? 0;
        var height = _game?.ScreenSizeHeight ?? 0;
        return new Rectangle(0, 0, width, height);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && !_isDisposed)
            {
                ReleaseTextureHandles();
                _isDisposed = true;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
