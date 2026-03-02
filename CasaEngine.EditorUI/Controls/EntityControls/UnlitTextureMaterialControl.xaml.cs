using System;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls;

/// <summary>
/// Control for editing <see cref="CasaEngine.Framework.Materials.UnlitTextureMaterial"/> properties.
/// Expects a <see cref="UnlitTextureMaterialViewModel"/> as DataContext.
/// </summary>
public partial class UnlitTextureMaterialControl : UserControl
{
    public UnlitTextureMaterialControl()
    {
        InitializeComponent();
    }

    public bool ValidateTextureAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is UnlitTextureMaterialViewModel vm &&
            System.IO.Path.GetExtension(assetFullName)
                .Equals(Constants.FileNameExtensions.Texture, StringComparison.OrdinalIgnoreCase))
        {
            vm.AlbedoAssetId = assetId;
            return true;
        }

        return false;
    }
}
