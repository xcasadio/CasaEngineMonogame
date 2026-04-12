using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using CasaEngine.Framework.UI.Backend.MonoGame.Primitives;
using MGUI.Shared.Assets;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaMonoGameBackendOptions
{
    public static Func<CasaDrawTransaction, IShapeRenderer2D> CreateAposShapeRendererFactory()
        => transaction => CreateOptionalAposShapeRenderer(transaction);

    public IUIAssetProvider? AssetProvider { get; init; }
    public ITextMeasurementEngine? TextEngine { get; init; }
    public Action<CasaBackendAdapterRegistry>? ConfigureAdapters { get; init; }
    public Func<CasaDrawTransaction, IShapeRenderer2D>? CreateShapeRenderer { get; init; }

    internal static CasaMonoGameBackendOptions Default { get; } = new();

    private static IShapeRenderer2D CreateOptionalAposShapeRenderer(CasaDrawTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        const string assemblyName = "CasaEngine.AposShapes";
        const string typeName = "CasaEngine.Framework.UI.Backend.MonoGame.Primitives.CasaAposShapeRenderer2D";
        string qualifiedTypeName = $"{typeName}, {assemblyName}";

        Type? rendererType = Type.GetType(qualifiedTypeName, throwOnError: false);
        if (rendererType == null)
        {
            throw new InvalidOperationException(
                $"The optional Apos renderer backend requires the '{assemblyName}' assembly. Reference CasaEngine.AposShapes to enable {nameof(CreateAposShapeRendererFactory)}().");
        }

        if (!typeof(IShapeRenderer2D).IsAssignableFrom(rendererType))
        {
            throw new InvalidOperationException($"Optional renderer type '{qualifiedTypeName}' does not implement {nameof(IShapeRenderer2D)}.");
        }

        if (Activator.CreateInstance(rendererType, transaction) is not IShapeRenderer2D renderer)
        {
            throw new InvalidOperationException($"Unable to instantiate optional renderer type '{qualifiedTypeName}'.");
        }

        return renderer;
    }
}