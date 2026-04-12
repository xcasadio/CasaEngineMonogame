using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaMonoGameBackendSession<THost>
    where THost : IRenderHost
{
    public THost Host { get; }
    public CasaDesktopRuntime Runtime { get; }

    internal CasaMonoGameBackendSession(THost host, CasaDesktopRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(runtime);
        Host = host;
        Runtime = runtime;
    }
}

public static class CasaMonoGameBackendBootstrap
{
    public static CasaMonoGameBackendSession<THost> Create<THost>(
        THost host,
        IRawInputSource? rawInputSource = null,
        IUISurface? surface = null,
        IUIAssetProvider? assetProvider = null,
        CasaMonoGameBackendOptions? options = null)
        where THost : IRenderHost
    {
        ArgumentNullException.ThrowIfNull(host);

        var runtime = new CasaDesktopRuntime(host, rawInputSource, surface, assetProvider, options);
        return new CasaMonoGameBackendSession<THost>(host, runtime);
    }
}