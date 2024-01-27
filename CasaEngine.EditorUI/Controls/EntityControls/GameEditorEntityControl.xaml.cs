using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logs;
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

    private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
    {
        if (EntityControlViewModel == null)
        {
            return;
        }

        var fileName = EntityControlViewModel.Entity.FileName;
        AssetSaver.SaveAsset(fileName, EntityControlViewModel.Entity);
        LogManager.Instance.WriteInfo($"Entity {EntityControlViewModel.Entity.Name} saved ({fileName})");
    }
}