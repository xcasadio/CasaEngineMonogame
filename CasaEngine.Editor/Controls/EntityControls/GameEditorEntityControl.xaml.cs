using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Controls.EntityControls
{
    public partial class GameEditorEntityControl : UserControl
    {
        private EntityDataViewModel? EntityControlViewModel => DataContext as EntityDataViewModel;

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

            AssetSaver.SaveAsset(EntityControlViewModel.FileName, EntityControlViewModel.Entity);
            LogManager.Instance.WriteLineInfo($"Entity {EntityControlViewModel.Entity.Name} saved ({EntityControlViewModel.FileName})");
        }
    }
}
