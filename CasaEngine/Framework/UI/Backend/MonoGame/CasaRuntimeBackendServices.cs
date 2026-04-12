using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal sealed class CasaRuntimeBackendServices
{
    public ContentManager Content { get; }
    public FontManager FontManager { get; }
    public IUIAssetProvider AssetProvider { get; }
    public CasaRenderTargetPool RenderTargetPool { get; } = new();
    public CasaBackendAdapterRegistry AdapterRegistry { get; }
    public CasaTextureCache TextureCache { get; }

    public CasaRuntimeBackendServices(
        IRenderHost host,
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        IUIAssetProvider? assetProvider,
        CasaMonoGameBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(options);

        Content = new ContentManager(host, "Content");
        FontManager = new FontManager(Content, "Arial");
        AssetProvider = assetProvider ?? options.AssetProvider ?? new CasaUIAssetProvider(Content, FontManager);
        TextureCache = new CasaTextureCache(graphicsDevice, spriteBatch);

        AdapterRegistry = new CasaBackendAdapterRegistry();
        AdapterRegistry.RegisterImageResource<CasaMonoGameImageResource>(resource => resource.Texture);
        AdapterRegistry.RegisterRenderTarget<CasaMonoGameRenderTarget>(resource => resource.RenderTarget);
        options.ConfigureAdapters?.Invoke(AdapterRegistry);
    }
}