using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            SelectedItem = ListBox.SelectedItem as SpriteDataViewModel;
        }

        public void InitializeFromGameEditor(GameEditorSprite gameEditorSprite)
        {
            _gameEditor = gameEditorSprite;
            _gameEditor.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            DataContext = new SpritesModelView(_gameEditor);
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
                    _gameEditor.Game.GameManager.AssetContentManager.Rename(spriteDataViewModel.Name, inputTextBox.Text);
                    spriteDataViewModel.Name = inputTextBox.Text;
                }
            }
        }

        public void LoadSpriteSheet(string fileName)
        {
            var spritesModelView = DataContext as SpritesModelView;
            spritesModelView.LoadSpriteSheet(fileName);

            if (spritesModelView.SpriteDatas.Count > 0)
            {
                Dispatcher.Invoke(() => ListBox.SelectedIndex = 0);
            }
        }
    }
}
