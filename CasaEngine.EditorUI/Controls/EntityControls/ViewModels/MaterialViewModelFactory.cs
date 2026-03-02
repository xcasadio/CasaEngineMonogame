using CasaEngine.Framework.Materials;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

/// <summary>
/// Creates the appropriate <see cref="MaterialViewModel"/> sub-class for a given <see cref="MaterialBase"/> instance.
/// </summary>
public static class MaterialViewModelFactory
{
    /// <summary>
    /// Returns a specialized <see cref="MaterialViewModel"/> matching the concrete type of
    /// <paramref name="material"/>, or <c>null</c> if <paramref name="material"/> is null.
    /// </summary>
    public static MaterialViewModel? Create(MaterialBase? material)
    {
        return material switch
        {
            null                    => null,
            UnlitTextureMaterial m  => new UnlitTextureMaterialViewModel(m),
            LitDiffuseMaterial m    => new LitDiffuseMaterialViewModel(m),
            Material m              => new PbrMaterialViewModel(m),
            _                       => new MaterialViewModel(material),
        };
    }
}
