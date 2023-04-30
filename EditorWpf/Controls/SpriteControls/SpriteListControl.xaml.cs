using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Framework.Assets.Map2d;
using EditorWpf.Controls.Common;

namespace EditorWpf.Controls.SpriteControls
{
    public partial class SpriteListControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(SpriteDataViewModel), typeof(SpriteListControl));
        private GameEditorSprite _gameEditorSprite;

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
            _gameEditorSprite = gameEditorSprite;
            _gameEditorSprite.GameStarted += OnGameStarted;
        }

        private void OnGameStarted(object? sender, System.EventArgs e)
        {
            DataContext = new SpritesModelView(_gameEditorSprite);
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
                    spriteDataViewModel.Name = inputTextBox.Text;
                }
            }
        }
    }
}
