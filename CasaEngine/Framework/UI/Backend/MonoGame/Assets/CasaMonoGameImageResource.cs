using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Assets;

public class CasaMonoGameImageResource : IUIImageResource
{
    public Texture2D Texture { get; }
    public int Width => Texture.Width;
    public int Height => Texture.Height;
    public bool IsDisposed => Texture.IsDisposed;

    public CasaMonoGameImageResource(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Texture = texture;
    }
}

public sealed class CasaMonoGameRenderTarget : CasaMonoGameImageResource, IUIRenderTarget
{
    public RenderTarget2D RenderTarget { get; }

    public CasaMonoGameRenderTarget(RenderTarget2D renderTarget)
        : base(renderTarget)
    {
        ArgumentNullException.ThrowIfNull(renderTarget);
        RenderTarget = renderTarget;
    }
}

internal static class CasaImageResourceTextureResolver
{
    private static readonly ConcurrentDictionary<Type, Func<IUIImageResource, Texture2D>?> Accessors = new();
    private static readonly ConcurrentDictionary<Type, Func<IUIRenderTarget, RenderTarget2D>?> RenderTargetAccessors = new();

    public static Texture2D GetTexture(IUIImageResource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image is CasaMonoGameImageResource casaImage)
        {
            return casaImage.Texture;
        }

        Func<IUIImageResource, Texture2D>? accessor = Accessors.GetOrAdd(image.GetType(), CreateAccessor);
        if (accessor != null)
        {
            return accessor(image);
        }

        throw new InvalidOperationException($"{nameof(IUIImageResource)} implementation '{image.GetType().FullName}' is not compatible with the CasaEngine MonoGame backend.");
    }

    public static RenderTarget2D GetRenderTarget(IUIRenderTarget renderTarget)
    {
        ArgumentNullException.ThrowIfNull(renderTarget);

        if (renderTarget is CasaMonoGameRenderTarget casaRenderTarget)
        {
            return casaRenderTarget.RenderTarget;
        }

        Func<IUIRenderTarget, RenderTarget2D>? accessor = RenderTargetAccessors.GetOrAdd(renderTarget.GetType(), CreateRenderTargetAccessor);
        if (accessor != null)
        {
            return accessor(renderTarget);
        }

        throw new InvalidOperationException($"{nameof(IUIRenderTarget)} implementation '{renderTarget.GetType().FullName}' is not compatible with the CasaEngine MonoGame backend.");
    }

    private static Func<IUIImageResource, Texture2D>? CreateAccessor(Type imageType)
    {
        var textureProperty = imageType.GetProperty(nameof(CasaMonoGameImageResource.Texture), typeof(Texture2D));
        if (textureProperty == null || !textureProperty.CanRead)
        {
            return null;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(IUIImageResource), "image");
        UnaryExpression cast = Expression.Convert(parameter, imageType);
        MemberExpression body = Expression.Property(cast, textureProperty);
        return Expression.Lambda<Func<IUIImageResource, Texture2D>>(body, parameter).Compile();
    }

    private static Func<IUIRenderTarget, RenderTarget2D>? CreateRenderTargetAccessor(Type renderTargetType)
    {
        var renderTargetProperty = renderTargetType.GetProperty(nameof(CasaMonoGameRenderTarget.RenderTarget), typeof(RenderTarget2D));
        if (renderTargetProperty == null || !renderTargetProperty.CanRead)
        {
            return null;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(IUIRenderTarget), "renderTarget");
        UnaryExpression cast = Expression.Convert(parameter, renderTargetType);
        MemberExpression body = Expression.Property(cast, renderTargetProperty);
        return Expression.Lambda<Func<IUIRenderTarget, RenderTarget2D>>(body, parameter).Compile();
    }
}