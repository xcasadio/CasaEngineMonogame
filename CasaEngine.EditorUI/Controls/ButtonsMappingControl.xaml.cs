using System.IO;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls
{
    public partial class ButtonsMappingControl : EditorControlBase
    {
        protected override string LayoutFileName { get; }
        public override DockingManager DockingManager { get; }

        public ButtonsMappingControl()
        {
            InitializeComponent();
        }

        protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
        {
            e.Content = e.Model.Title switch
            {
                /*"Animations 2d" => Animation2dListControl,
                "Animation 2d View" => GameEditorAnimation2dControl,
                "Details" => animation2dDetailsControl,*/
                "Logs" => this.FindParent<MainWindow>().LogsControl,
                "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
                _ => e.Content
            };
        }

        public void OpenButtonsMapping(string fileName)
        {
            var assetLoader = new AssetLoader<ButtonsMapping>();
            var buttonsMapping = (ButtonsMapping)assetLoader.LoadAsset(Path.Combine(EngineEnvironment.ProjectPath, fileName), null);
            //TODO remove
            buttonsMapping.Buttons.Add(new InputMapping { Name = "test" });
            DataContext = buttonsMapping.Buttons[0];
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is ButtonsMapping buttonsMapping)
            {
                AssetSaver.SaveAsset(buttonsMapping.FileName, buttonsMapping);
                Logs.WriteInfo($"Buttons mapping {buttonsMapping.Name} saved ({buttonsMapping.FileName})");
            }
        }
    }
}
