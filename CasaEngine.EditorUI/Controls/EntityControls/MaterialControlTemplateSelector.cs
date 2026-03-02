using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

namespace CasaEngine.EditorUI.Controls.EntityControls;

/// <summary>
/// Selects the correct material editing <see cref="DataTemplate"/> based on the
/// concrete <see cref="MaterialViewModel"/> sub-type.
/// </summary>
public class MaterialControlTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UnlitTextureMaterialTemplate { get; set; }
    public DataTemplate? LitDiffuseMaterialTemplate   { get; set; }
    public DataTemplate? PbrMaterialTemplate          { get; set; }
    public DataTemplate? DefaultMaterialTemplate      { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            UnlitTextureMaterialViewModel => UnlitTextureMaterialTemplate,
            LitDiffuseMaterialViewModel   => LitDiffuseMaterialTemplate,
            PbrMaterialViewModel          => PbrMaterialTemplate,
            MaterialViewModel             => DefaultMaterialTemplate,
            _                             => DefaultMaterialTemplate,
        };
    }
}
