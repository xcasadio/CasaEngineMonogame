using System.Windows;
using System.Windows.Controls;

namespace CasaEngine.EditorUI.Controls.EntityControls;

/// <summary>
/// User control that displays base <see cref="CasaEngine.Framework.Materials.MaterialBase"/>
/// properties (render states, flags, queue).
/// Expects a <see cref="CasaEngine.EditorUI.Controls.EntityControls.ViewModels.MaterialViewModel"/>
/// as DataContext.
/// </summary>
public partial class MaterialBaseControl : UserControl
{
    public MaterialBaseControl()
    {
        InitializeComponent();
    }

    // Save is implemented in Task 13 — stub kept here for XAML compilation.
    private void SaveMaterial_Click(object sender, RoutedEventArgs e)
    {
        // Implemented in Task 13
    }
}
