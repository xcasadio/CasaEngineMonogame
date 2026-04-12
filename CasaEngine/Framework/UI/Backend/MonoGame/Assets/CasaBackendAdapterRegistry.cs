using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Assets;

public sealed class CasaBackendAdapterRegistry
{
    private interface IImageAdapter
    {
        Type ResourceType { get; }
        Texture2D GetTexture(IUIImageResource resource);
    }

    private interface IRenderTargetAdapter
    {
        Type ResourceType { get; }
        RenderTarget2D GetRenderTarget(IUIRenderTarget resource);
    }

    private sealed class ImageAdapter<TResource> : IImageAdapter
        where TResource : class, IUIImageResource
    {
        private readonly Func<TResource, Texture2D> _accessor;

        public ImageAdapter(Func<TResource, Texture2D> accessor)
        {
            ArgumentNullException.ThrowIfNull(accessor);
            _accessor = accessor;
        }

        public Type ResourceType => typeof(TResource);

        public Texture2D GetTexture(IUIImageResource resource) => _accessor((TResource)resource);
    }

    private sealed class RenderTargetAdapter<TResource> : IRenderTargetAdapter
        where TResource : class, IUIRenderTarget
    {
        private readonly Func<TResource, RenderTarget2D> _accessor;

        public RenderTargetAdapter(Func<TResource, RenderTarget2D> accessor)
        {
            ArgumentNullException.ThrowIfNull(accessor);
            _accessor = accessor;
        }

        public Type ResourceType => typeof(TResource);

        public RenderTarget2D GetRenderTarget(IUIRenderTarget resource) => _accessor((TResource)resource);
    }

    private readonly List<IImageAdapter> _imageAdapters = new();
    private readonly List<IRenderTargetAdapter> _renderTargetAdapters = new();
    private readonly Dictionary<Type, IImageAdapter?> _imageAdapterCache = new();
    private readonly Dictionary<Type, IRenderTargetAdapter?> _renderTargetAdapterCache = new();

    public void RegisterImageResource<TResource>(Func<TResource, Texture2D> textureAccessor)
        where TResource : class, IUIImageResource
    {
        ArgumentNullException.ThrowIfNull(textureAccessor);

        RemoveAdapters(_imageAdapters, typeof(TResource));
        _imageAdapters.Add(new ImageAdapter<TResource>(textureAccessor));
        _imageAdapterCache.Clear();
    }

    public void RegisterRenderTarget<TResource>(Func<TResource, RenderTarget2D> renderTargetAccessor)
        where TResource : class, IUIRenderTarget
    {
        ArgumentNullException.ThrowIfNull(renderTargetAccessor);

        RemoveAdapters(_renderTargetAdapters, typeof(TResource));
        _renderTargetAdapters.Add(new RenderTargetAdapter<TResource>(renderTargetAccessor));
        _renderTargetAdapterCache.Clear();
    }

    public Texture2D GetTexture(IUIImageResource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        Type resourceType = image.GetType();
        if (!_imageAdapterCache.TryGetValue(resourceType, out IImageAdapter? adapter))
        {
            adapter = ResolveAdapter(_imageAdapters, resourceType);
            _imageAdapterCache[resourceType] = adapter;
        }

        if (adapter == null)
        {
            throw new InvalidOperationException($"{nameof(IUIImageResource)} implementation '{resourceType.FullName}' is not registered with the CasaEngine MonoGame backend adapter registry.");
        }

        return adapter.GetTexture(image);
    }

    public RenderTarget2D GetRenderTarget(IUIRenderTarget renderTarget)
    {
        ArgumentNullException.ThrowIfNull(renderTarget);

        Type resourceType = renderTarget.GetType();
        if (!_renderTargetAdapterCache.TryGetValue(resourceType, out IRenderTargetAdapter? adapter))
        {
            adapter = ResolveAdapter(_renderTargetAdapters, resourceType);
            _renderTargetAdapterCache[resourceType] = adapter;
        }

        if (adapter == null)
        {
            throw new InvalidOperationException($"{nameof(IUIRenderTarget)} implementation '{resourceType.FullName}' is not registered with the CasaEngine MonoGame backend adapter registry.");
        }

        return adapter.GetRenderTarget(renderTarget);
    }

    private static void RemoveAdapters<TAdapter>(List<TAdapter> adapters, Type resourceType)
        where TAdapter : class
    {
        for (int index = adapters.Count - 1; index >= 0; index--)
        {
            object adapter = adapters[index]!;
            Type adapterType = adapter switch
            {
                IImageAdapter imageAdapter => imageAdapter.ResourceType,
                IRenderTargetAdapter renderTargetAdapter => renderTargetAdapter.ResourceType,
                _ => throw new NotSupportedException($"Unsupported adapter instance '{adapter.GetType().FullName}'."),
            };

            if (adapterType == resourceType)
            {
                adapters.RemoveAt(index);
            }
        }
    }

    private static TAdapter? ResolveAdapter<TAdapter>(IEnumerable<TAdapter> adapters, Type resourceType)
        where TAdapter : class
    {
        foreach (TAdapter adapter in adapters)
        {
            Type adapterType = adapter switch
            {
                IImageAdapter imageAdapter => imageAdapter.ResourceType,
                IRenderTargetAdapter renderTargetAdapter => renderTargetAdapter.ResourceType,
                _ => throw new NotSupportedException($"Unsupported adapter instance '{adapter.GetType().FullName}'."),
            };

            if (adapterType.IsAssignableFrom(resourceType))
            {
                return adapter;
            }
        }

        return null;
    }
}