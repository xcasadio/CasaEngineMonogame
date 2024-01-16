using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logs;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Controls.EntityControls;

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

        var fileName = EntityControlViewModel.Entity.AssetInfo.FileName;
        AssetSaver.SaveAsset(fileName, EntityControlViewModel.Entity);
        LogManager.Instance.WriteInfo($"Entity {EntityControlViewModel.Entity.Name} saved ({fileName})");
    }
}