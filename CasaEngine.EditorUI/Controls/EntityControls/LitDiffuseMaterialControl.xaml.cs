using System;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls;

/// <summary>
/// Control for editing <see cref="CasaEngine.Framework.Materials.LitDiffuseMaterial"/> properties.
/// Expects a <see cref="LitDiffuseMaterialViewModel"/> as DataContext.
/// </summary>
public partial class LitDiffuseMaterialControl : UserControl
{
    public LitDiffuseMaterialControl()
    {
        InitializeComponent();
    }

    public bool ValidateTextureAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is LitDiffuseMaterialViewModel vm &&
            System.IO.Path.GetExtension(assetFullName)
                .Equals(Constants.FileNameExtensions.Texture, StringComparison.OrdinalIgnoreCase))
        {
            vm.AlbedoAssetId = assetId;
            return true;
        }

        return false;
    }

    public bool ValidateNormalMapAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is LitDiffuseMaterialViewModel vm &&
            System.IO.Path.GetExtension(assetFullName)
                .Equals(Constants.FileNameExtensions.Texture, StringComparison.OrdinalIgnoreCase))
        {
            vm.NormalMapAssetId = assetId;
            return true;
        }

        return false;
    }
}
