using System;
using System.Windows.Controls;
using CasaEngine.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls;

/// <summary>
/// Control for editing <see cref="CasaEngine.Framework.Materials.Material"/> (PBR) properties.
/// Expects a <see cref="CasaEngine.EditorUI.Controls.EntityControls.ViewModels.PbrMaterialViewModel"/> as DataContext.
/// </summary>
public partial class PbrMaterialControl : UserControl
{
    public PbrMaterialControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Single validation delegate shared by all 8 texture slots.
    /// The TwoWay binding on AssetId propagates the selected Guid to the correct ViewModel property.
    /// </summary>
    public bool ValidateTextureAsset(object owner, Guid assetId, string assetFullName)
    {
        return System.IO.Path.GetExtension(assetFullName)
            .Equals(Constants.FileNameExtensions.Texture, StringComparison.OrdinalIgnoreCase);
    }
}
