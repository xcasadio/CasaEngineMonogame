using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using EditorWpf.Controls.Common;

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
            var selectedItem = ListBox.SelectedItem as AssetInfoViewModel;
            var spriteData = _gameEditor.Game.GameManager.AssetContentManager.Load<SpriteData>(selectedItem.AssetInfo, _gameEditor.Game.GraphicsDevice);
            SelectedItem = new SpriteDataViewModel(spriteData);
        }

        public void InitializeFromGameEditor(GameEditorSprite gameEditorSprite)
        {
            _gameEditor = gameEditorSprite;
            _gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, EventArgs e)
        {
            DataContext = new SpritesModelView();
        }

        private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                var inputTextBox = new InputTextBox();
                inputTextBox.Description = "Enter a new name";
                inputTextBox.Title = "Rename";
                var spriteDataViewModel = (listBox.SelectedItem as AssetInfoViewModel);
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

            for (int i = 0; i < spritesModelView.SpriteAssetInfos.Count; i++)
            {
                if (spritesModelView.SpriteAssetInfos[i].AssetInfo.FileName == fileName)
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
                var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, spriteDataViewModel.AssetInfo.FileName);
                AssetSaver.SaveAsset(fullFileName, spriteDataViewModel.SpriteData);
            }
        }
    }
}
