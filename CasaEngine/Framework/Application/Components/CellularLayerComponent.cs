using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Drives <see cref="CellularLayers.CellularLayerService"/> from the game loop: resolves the service's
/// per-<c>PalDex</c> tile-sheet texture ids into <see cref="Texture2D"/>s, advances the pending frame
/// and submits every visible cell through <see cref="SpriteRendererComponent"/>. Same house pattern as
/// <see cref="ScrollingLayerComponent"/> - a parallel sibling, not an extension of it
/// (docs/engine/cellular-layers.md, plan-e9d-mode-cellulaire.md D3).
///
/// Submission happens from this component's own <see cref="Update"/>, not <c>Draw</c> - same reasoning
/// as <see cref="ScrollingLayerComponent"/>: the game DLL pushes this frame's camera/ticks to
/// <see cref="Service"/> from <c>GameManager.UpdateWorld</c>, which every <c>GameComponent.Update</c>
/// (this one included) runs after, so the state read here is already final for the frame.
/// </summary>
public class CellularLayerComponent : GameComponent
{
    private readonly CasaEngineGame _game;
    private Texture2D[][] _layerSheets = System.Array.Empty<Texture2D[]>();
    private int _resolvedLayersVersion = -1;

    public CellularLayerComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game as CasaEngineGame;
        Service = new CellularLayerService();

        UpdateOrder = (int)ComponentUpdateOrder.CellularLayers;
        game.Components.Add(this);
    }

    /// <summary>Layer/cell definitions, per-tick state and the frame push contract.</summary>
    public CellularLayerService Service { get; }

    /// <summary>
    /// D7: the source of the next raw 32-bit value for <see cref="CellularCellType.FallRespawn"/>'s
    /// respawn - the original's own global random stream (<c>Random.cs:5,14</c>, seed <c>0xB017C93D</c>,
    /// <c>seed = seed * 0x7d2b89dd + 0xe06a02e7</c>), which the game DLL owns and this engine-side
    /// component does not have access to. The consuming DLL's integration slice (C3) must set this to
    /// that shared stream before any <see cref="CellularCellType.FallRespawn"/> layer's cell can
    /// actually respawn - <see cref="Service"/>'s <see cref="CellularLayerService.Advance"/> only calls
    /// this delegate lazily, on the tick a respawn actually happens, so a world with no
    /// <see cref="CellularCellType.FallRespawn"/> cells never reaches the default below.
    ///
    /// The default warns ONCE and returns 0 rather than throwing: this runs on the per-frame render
    /// path, where the engine's rules forbid both throwing and logging every frame. An unwired
    /// stream is a wiring mistake, not a data error - it is reported, and the affected cells respawn
    /// at abscissa 0 instead of taking the whole frame down.
    /// </summary>
    public System.Func<uint> RandomSource { get; set; } = WarnOnceAndReturnZero;

    private static bool _warnedAboutUnwiredRandomSource;

    private static uint WarnOnceAndReturnZero()
    {
        if (!_warnedAboutUnwiredRandomSource)
        {
            _warnedAboutUnwiredRandomSource = true;
            Logs.WriteWarning(
                "CellularLayerComponent.RandomSource is not wired: FallRespawn cells will respawn at "
                + "abscissa 0. D7 requires this to share the gameplay DLL's own global random stream.");
        }

        return 0u;
    }

    public override void Update(GameTime gameTime)
    {
        if (_resolvedLayersVersion != Service.LayersVersion)
        {
            ResolveTextures(LoadTexture);
            _resolvedLayersVersion = Service.LayersVersion;
        }

        if (Service.HasPendingFrame)
        {
            Service.Advance(RandomSource);
        }

        if (Service.FramesPushed > 0 && _game?.SpriteRendererComponent != null)
        {
            var scissorRectangle = ResolveScissorRectangle();
            Submit(_game.SpriteRendererComponent, Service.CameraTarget, scissorRectangle);
        }

        base.Update(gameTime);
    }

    /// <summary>
    /// Resolves every current layer's per-<c>PalDex</c> tile-sheet textures through
    /// <paramref name="loader"/> (production: <c>AssetContentManager.Load&lt;Texture&gt;</c> +
    /// <c>.Load(acm)</c> + <c>.Resource</c>). A null/empty id resolves to a null texture directly
    /// without calling <paramref name="loader"/>; a texture that fails to load is logged and skipped -
    /// any cell referencing it is simply not drawn (no partial-array fallback chain: unlike the sibling
    /// mechanism's V-animation frames, a cellular layer's sheets are independent per <c>PalDex</c>, not
    /// an ordered sequence). Called by <see cref="Update"/> whenever <see cref="CellularLayerService.LayersVersion"/>
    /// changes - never per frame - and explicitly resets every layer's per-tick runtime state at that
    /// same occasion (<see cref="CellularLayerService.ResetLayerRuntimeState"/>, same D-E9b-7 rationale
    /// as the sibling mechanism).
    /// </summary>
    public void ResolveTextures(System.Func<System.Guid, Texture2D> loader)
    {
        var layerCount = Service.LayerCount;
        var layerSheets = new Texture2D[layerCount][];

        for (var i = 0; i < layerCount; i++)
        {
            var definition = Service.GetLayerDefinition(i);
            var sheetIds = definition.SheetTextureAssetIds ?? System.Array.Empty<System.Guid>();
            var sheets = new Texture2D[sheetIds.Length];

            for (var s = 0; s < sheetIds.Length; s++)
            {
                var id = sheetIds[s];
                if (id == System.Guid.Empty)
                {
                    continue;
                }

                var texture = loader(id);
                if (texture == null)
                {
                    Logs.WriteWarning(
                        $"CellularLayerComponent: layer index {i} (LayerId {definition.LayerId}) sheet "
                        + $"PalDex {s} texture '{id}' failed to load; cells using it are skipped.");
                    continue;
                }

                sheets[s] = texture;
            }

            layerSheets[i] = sheets;
        }

        _layerSheets = layerSheets;
        Service.ResetLayerRuntimeState();
    }

    private Texture2D LoadTexture(System.Guid id)
    {
        var wrapperTexture = _game?.AssetContentManager.Load<CasaEngineTexture>(id);
        wrapperTexture?.Load(_game.AssetContentManager);
        return wrapperTexture?.Resource;
    }

    /// <summary>
    /// Submits every visible cell of every layer, through the scissor-explicit keyed <c>DrawSprite</c>
    /// overload - never the device's current scissor rectangle, so this method never touches
    /// <see cref="GraphicsDevice"/> and is exercisable headless. Skips a cell whose
    /// <see cref="CellularCellState.ShouldDraw"/> is false (<see cref="CellularCellType.ScriptTrack"/>,
    /// or a <see cref="CellularCellType.WaveX"/> cell reached while the map's <c>WaveLut</c> was empty)
    /// and a cell whose <c>PalDex</c> sheet failed to resolve. Submits nothing if
    /// <see cref="CellularLayerService.FramesPushed"/> is still 0.
    /// </summary>
    public void Submit(SpriteRendererComponent renderer, Vector3 cameraTarget, Rectangle scissorRectangle)
    {
        if (renderer == null || Service.FramesPushed == 0)
        {
            return;
        }

        var halfWidth = CellularLayerService.ScreenWidth / 2f;
        var halfHeight = CellularLayerService.ScreenHeight / 2f;
        var configuration = Service.Configuration;

        for (var i = 0; i < Service.LayerCount; i++)
        {
            if (!Service.TryGetLayerState(i, out var layerState))
            {
                continue;
            }

            var definition = Service.GetLayerDefinition(i);
            var sheets = i < _layerSheets.Length ? _layerSheets[i] : System.Array.Empty<Texture2D>();

            // D6: the render pass is derived from Ground, exactly like the DLL already routes the
            // sibling mechanism's own layers (AlundraBackdropStage.BuildDefinitions).
            var pass = definition.Ground ? RenderPass2D.Effects : RenderPass2D.Background;
            var sortKey = new RenderSortKey2D((int)pass, definition.SortingLayer, definition.OrderInLayer, 0, 0, 0, definition.LayerId);
            var layerZ = pass == RenderPass2D.Background
                ? cameraTarget.Z - configuration.BackgroundDepth
                : cameraTarget.Z;

            var cellCount = Service.GetCellCount(i);
            for (var c = 0; c < cellCount; c++)
            {
                if (!Service.TryGetCellState(i, c, out var cellState) || !cellState.ShouldDraw)
                {
                    continue;
                }

                var cellDefinition = definition.Cells[c];
                var sheet = cellDefinition.PalDex < sheets.Length ? sheets[cellDefinition.PalDex] : null;
                if (sheet == null)
                {
                    continue;
                }

                var v = CellularLayerService.ComputeSourceV(cellDefinition.V0, layerState.Phase);
                var width = cellDefinition.U1 - cellDefinition.U0 + 1;
                var height = cellDefinition.V1 - cellDefinition.V0 + 1;
                var sourceRectangle = new Rectangle(cellDefinition.U0, v, width, height);

                var worldPosition = new Vector2(
                    cameraTarget.X + (cellState.DrawX - halfWidth),
                    cameraTarget.Y + (halfHeight - cellState.DrawY));

                renderer.DrawSprite(
                    sheet,
                    sourceRectangle,
                    Point.Zero,
                    worldPosition,
                    0f,
                    Vector2.One,
                    definition.Tint,
                    layerZ,
                    sortKey,
                    SpriteEffects.None,
                    scissorRectangle,
                    definition.Blend);
            }
        }
    }

    private Rectangle ResolveScissorRectangle()
    {
        GraphicsDevice graphicsDevice;
        try
        {
            // Same guard as ScrollingLayerComponent/ScreenEffectComponent: a headless/partially
            // constructed Game can throw from the GraphicsDevice getter itself rather than returning
            // null - either way, no device means fall back to the pixel-size rectangle below.
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
}
