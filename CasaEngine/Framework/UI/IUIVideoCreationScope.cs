using MGUI.Shared.Assets;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Implemented by an asset provider that can tell which videos it created during a stretch of time. A screen opens a
/// scope around the load of its XAML, so that when the load fails, the videos the elements had already created (each
/// one owns a texture and an open file) can be disposed instead of staying in the provider until it is released.
/// Engine-internal: it is not part of <see cref="IUIAssetProvider"/>.
/// </summary>
public interface IUIVideoCreationScope
{
    /// <summary>Starts recording into <paramref name="created"/> every video the provider creates. A scope opened while
    /// another is open takes over; <see cref="EndVideoCreationScope"/> then gives the previous one back.</summary>
    void BeginVideoCreationScope(List<IUIVideo> created);

    /// <summary>Ends the innermost open scope.</summary>
    void EndVideoCreationScope();
}
