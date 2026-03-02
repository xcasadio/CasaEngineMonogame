using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

namespace CasaEngine.EditorUI.Controls.EntityControls;

/// <summary>
/// User control that displays base <see cref="CasaEngine.Framework.Materials.MaterialBase"/>
/// properties (render states, flags, queue).
/// Expects a <see cref="MaterialViewModel"/> as DataContext.
/// </summary>
public partial class MaterialBaseControl : UserControl
{
    public MaterialBaseControl()
    {
        InitializeComponent();
    }

    private void SaveMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MaterialViewModel vm)
        {
            vm.SaveMaterial();
        }
    }
}
