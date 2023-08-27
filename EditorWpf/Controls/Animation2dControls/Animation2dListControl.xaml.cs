using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using EditorWpf.Controls.Common;
using EditorWpf.Controls.SpriteControls;

namespace EditorWpf.Controls.Animation2dControls
{
    public partial class Animation2dListControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(Animation2dDataViewModel), typeof(Animation2dListControl));
        private GameEditorAnimation2d _gameEditor;

        public Animation2dDataViewModel? SelectedItem
        {
            get => (Animation2dDataViewModel)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public Animation2dListControl()
        {
            InitializeComponent();
            SortAnimations();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ListBox.SelectedItem as AssetInfoViewModel;
            var animation2dData = LoadAnimation(selectedItem.AssetInfo);
            SelectedItem = new Animation2dDataViewModel(animation2dData);
        }

        private Animation2dData LoadAnimation(AssetInfo assetInfo)
        {
            var assetContentManager = _gameEditor.Game.GameManager.AssetContentManager;
            var graphicsDevice = _gameEditor.Game.GraphicsDevice;
            var animation2dData = assetContentManager.Load<Animation2dData>(assetInfo, graphicsDevice);

            foreach (var frameData in animation2dData.Frames)
            {
                var frameAssetInfo = GameSettings.AssetInfoManager.Get(frameData.SpriteId);
                assetContentManager.Load<SpriteData>(frameAssetInfo, graphicsDevice);
            }

            return animation2dData;
        }

        public void InitializeFromGameEditor(GameEditorAnimation2d gameEditor)
        {
            _gameEditor = gameEditor;
            _gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            DataContext = new Animation2dListModelView();
        }

        private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                var inputTextBox = new InputTextBox();
                inputTextBox.Description = "Enter a new name";
                inputTextBox.Title = "Rename";
                var animation2dDataViewModel = (listBox.SelectedItem as AssetInfoViewModel);
                inputTextBox.Text = animation2dDataViewModel.Name;

                if (inputTextBox.ShowDialog() == true)
                {
                    //_gameEditor.Game.GameManager.AssetContentManager.Rename(animation2dDataViewModel.Name, inputTextBox.Text);
                    animation2dDataViewModel.Name = inputTextBox.Text;
                    SortAnimations();
                    listBox.ScrollIntoView(listBox.SelectedItem);
                }
            }
        }

        private void SortAnimations()
        {
            ListBox.Items.SortDescriptions.Clear();
            ListBox.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        public void OpenAnimations2d(string fileName)
        {
            var animation2dListModelView = DataContext as Animation2dListModelView;

            foreach (var assetInfoViewModel in animation2dListModelView.Animation2dAssetInfos)
            {
                if (fileName.EndsWith(assetInfoViewModel.AssetInfo.FileName))
                {
                    var index = ListBox.Items.IndexOf(assetInfoViewModel);
                    Dispatcher.Invoke(() => ListBox.SelectedIndex = index);
                    break;
                }
            }
        }

        public void SaveCurrentAnimation()
        {
            if (SelectedItem is Animation2dDataViewModel animation2dDataViewModel)
            {
                var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, animation2dDataViewModel.AssetInfo.FileName);
                AssetSaver.SaveAsset(fullFileName, animation2dDataViewModel.Animation2dData);
            }
        }
    }
}