using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
            var selectedItem = ListBox.SelectedItem as Animation2dDataViewModel;
            LoadAnimation(selectedItem.AssetInfo);
            SelectedItem = selectedItem;
        }

        private void LoadAnimation(AssetInfo assetInfo)
        {
            var assetContentManager = _gameEditor.Game.GameManager.AssetContentManager;
            var graphicsDevice = _gameEditor.Game.GraphicsDevice;
            var animation2dData = assetContentManager.Load<Animation2dData>(assetInfo, graphicsDevice);

            foreach (var frameData in animation2dData.Frames)
            {
                var frameAssetInfo = GameSettings.AssetInfoManager.Get(frameData.SpriteId);
                assetContentManager.Load<SpriteData>(frameAssetInfo, graphicsDevice);
            }
        }

        public void InitializeFromGameEditor(GameEditorAnimation2d gameEditor)
        {
            _gameEditor = gameEditor;
            _gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            GameSettings.AssetInfoManager.AssetAdded += OnAssetAdded;
            GameSettings.AssetInfoManager.AssetRemoved += OnAssetRemoved;
            GameSettings.AssetInfoManager.AssetCleared += OnAssetCleared;
            DataContext = new Animation2dListModelView(_gameEditor);
        }
        private void OnAssetAdded(object? sender, AssetInfo assetInfo)
        {
            if (Path.GetExtension(assetInfo.FileName) == Constants.FileNameExtensions.Animation2d)
            {
                var animation2dListModelView = DataContext as Animation2dListModelView;
                animation2dListModelView.Add(assetInfo);
            }
        }

        private void OnAssetRemoved(object? sender, AssetInfo assetInfo)
        {
            var animation2dListModelView = DataContext as Animation2dListModelView;

            var spriteDataViewModel = animation2dListModelView.Animation2dDatas.FirstOrDefault(x => x.Name == assetInfo.Name); // by id
            if (spriteDataViewModel != null)
            {
                animation2dListModelView.Animation2dDatas.Remove(spriteDataViewModel);
            }
        }

        private void OnAssetCleared(object? sender, EventArgs e)
        {
            var animation2dListModelView = DataContext as Animation2dListModelView;
            animation2dListModelView.Animation2dDatas.Clear();
        }

        private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                var inputTextBox = new InputTextBox();
                inputTextBox.Description = "Enter a new name";
                inputTextBox.Title = "Rename";
                var animation2dDataViewModel = (listBox.SelectedItem as Animation2dDataViewModel);
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
            var index = -1;

            for (int i = 0; i < animation2dListModelView.Animation2dDatas.Count; i++)
            {
                if (animation2dListModelView.Animation2dDatas[i].AssetInfo.FileName == fileName)
                {
                    index = i;
                    break;
                }
            }

            if (index != -1)
            {
                Dispatcher.Invoke(() => ListBox.SelectedIndex = index);
            }
        }

        public void SaveCurrentAnimation()
        {
            if (SelectedItem is Animation2dDataViewModel animation2dDataViewModel)
            {
                var fullFileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, animation2dDataViewModel.AssetInfo.FileName);
                AssetSaver.SaveAsset(fullFileName, animation2dDataViewModel.Animation2dData);
            }
        }
    }
}