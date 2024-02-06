using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class GameEditorEntityControl : UserControl
{
    private EntityViewModel? EntityControlViewModel => DataContext as EntityViewModel;

    public GameEditorEntityControl()
    {
        InitializeComponent();
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (EntityControlViewModel == null)
        {
            return;
        }

        var fileName = EntityControlViewModel.Entity.FileName;
        AssetSaver.SaveAsset(fileName, EntityControlViewModel.Entity);
        Logs.WriteInfo($"Entity {EntityControlViewModel.Entity.Name} saved ({fileName})");
    }
}