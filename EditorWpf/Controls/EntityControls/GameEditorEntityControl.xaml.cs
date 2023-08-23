using System.IO;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EditorWpf.Controls.EntityControls
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

            var jObject = new JObject();

            EntityControlViewModel.Entity.Save(jObject, SaveOption.Editor);

            using StreamWriter file = File.CreateText(EntityControlViewModel.FileName);
            using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
            jObject.WriteTo(writer);

            LogManager.Instance.WriteLineInfo($"Entity {EntityControlViewModel.Entity.Name} saved ({EntityControlViewModel.FileName})");
        }
    }
}
