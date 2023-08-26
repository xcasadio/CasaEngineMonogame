using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Design;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using EditorWpf.Controls.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EditorWpf.Controls.SpriteControls
{
    public partial class SpriteListControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(SpriteDataViewModel), typeof(SpriteListControl));

        private GameEditorSprite _gameEditor;

        public SpriteDataViewModel SelectedItem
        {
            get => (SpriteDataViewModel)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public SpriteListControl()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ListBox.SelectedItem as SpriteDataViewModel;
            _gameEditor.Game.GameManager.AssetContentManager.Load<SpriteData>(selectedItem.AssetInfo, _gameEditor.Game.GraphicsDevice);
            SelectedItem = selectedItem;
        }

        public void InitializeFromGameEditor(GameEditorSprite gameEditorSprite)
        {
            _gameEditor = gameEditorSprite;
            _gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            GameSettings.AssetInfoManager.AssetAdded += OnAssetAdded;
            GameSettings.AssetInfoManager.AssetRemoved += OnAssetRemoved;
            GameSettings.AssetInfoManager.AssetCleared += OnAssetCleared;
            DataContext = new SpritesModelView(_gameEditor);
        }

        private void OnAssetAdded(object? sender, AssetInfo assetInfo)
        {
            if (Path.GetExtension(assetInfo.FileName) == Constants.FileNameExtensions.Sprite)
            {
                var spritesModelView = DataContext as SpritesModelView;
                spritesModelView.Add(assetInfo);
            }
        }

        private void OnAssetRemoved(object? sender, AssetInfo assetInfo)
        {
            var spritesModelView = DataContext as SpritesModelView;

            var spriteDataViewModel = spritesModelView.SpriteDatas.FirstOrDefault(x => x.Name == assetInfo.Name); // by id
            if (spriteDataViewModel != null)
            {
                spritesModelView.SpriteDatas.Remove(spriteDataViewModel);
            }
        }

        private void OnAssetCleared(object? sender, EventArgs e)
        {
            var spritesModelView = DataContext as SpritesModelView;
            spritesModelView.SpriteDatas.Clear();
        }

        private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                var inputTextBox = new InputTextBox();
                inputTextBox.Description = "Enter a new name";
                inputTextBox.Title = "Rename";
                var spriteDataViewModel = (listBox.SelectedItem as SpriteDataViewModel);
                inputTextBox.Text = spriteDataViewModel.Name;

                if (inputTextBox.ShowDialog() == true)
                {
                    //_gameEditor.Game.GameManager.AssetContentManager.Rename(spriteDataViewModel.Name, inputTextBox.Text);
                    spriteDataViewModel.Name = inputTextBox.Text;
                }
            }
        }

        public void OpenSprite(string fileName)
        {
            var spritesModelView = DataContext as SpritesModelView;
            var index = -1;

            for (int i = 0; i < spritesModelView.SpriteDatas.Count; i++)
            {
                if (spritesModelView.SpriteDatas[i].AssetInfo.FileName == fileName)
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

        public void SaveCurrentSprite()
        {
            if (SelectedItem is SpriteDataViewModel spriteDataViewModel)
            {
                var fullFileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, spriteDataViewModel.AssetInfo.FileName);
                AssetSaver.SaveAsset(fullFileName, spriteDataViewModel.SpriteData);
            }
        }
    }
}
